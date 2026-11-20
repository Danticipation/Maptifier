package com.maptifier.core;

import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.util.Log;
import java.io.IOException;
import java.nio.ByteBuffer;

/**
 * Native Android video encoder using MediaCodec and MediaMuxer.
 * Uses Input Buffers for maximum compatibility across devices without requiring complex EGL bridging.
 */
public class MaptifierEncoder {
    private static final String TAG = "MaptifierEncoder";
    private static final String MIME_TYPE = "video/avc";
    private static final int IFRAME_INTERVAL = 2;

    private MediaCodec mEncoder;
    private MediaMuxer mMuxer;
    private int mTrackIndex;
    private boolean mMuxerStarted;
    private MediaCodec.BufferInfo mBufferInfo;
    private int mWidth;
    private int mHeight;
    private byte[] mYuvBuffer;

    public void init(String outputPath, int width, int height, int bitRate, int frameRate) throws IOException {
        Log.d(TAG, "Initializing InputBuffer encoder: " + width + "x" + height);
        
        mWidth = width;
        mHeight = height;
        mBufferInfo = new MediaCodec.BufferInfo();
        // YUV420SP buffer size: width*height for Y, width*height/2 for UV
        mYuvBuffer = new byte[width * height * 3 / 2];

        MediaFormat format = MediaFormat.createVideoFormat(MIME_TYPE, width, height);
        format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatYUV420Flexible);
        format.setInteger(MediaFormat.KEY_BIT_RATE, bitRate);
        format.setInteger(MediaFormat.KEY_FRAME_RATE, frameRate);
        format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, IFRAME_INTERVAL);

        mEncoder = MediaCodec.createEncoderByType(MIME_TYPE);
        mEncoder.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
        mEncoder.start();

        mMuxer = new MediaMuxer(outputPath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
        mTrackIndex = -1;
        mMuxerStarted = false;
    }

    public void encodeFrame(byte[] rgbaData, long presentationTimeUs) {
        // Convert RGBA to YUV420SemiPlanar (NV21/NV12 style)
        // This is a simple software conversion. For production at scale, 
        // a specialized shader or native C++ conversion is faster, but this works for the prototype phase.
        encodeYUV420SP(mYuvBuffer, rgbaData, mWidth, mHeight);

        int inputBufferIndex = mEncoder.dequeueInputBuffer(10000);
        if (inputBufferIndex >= 0) {
            ByteBuffer inputBuffer = mEncoder.getInputBuffer(inputBufferIndex);
            inputBuffer.clear();
            inputBuffer.put(mYuvBuffer);
            mEncoder.queueInputBuffer(inputBufferIndex, 0, mYuvBuffer.length, presentationTimeUs, 0);
        }

        drainEncoder(false);
    }

    private void drainEncoder(boolean endOfStream) {
        if (endOfStream) {
            mEncoder.signalEndOfStream();
        }

        while (true) {
            int encoderStatus = mEncoder.dequeueOutputBuffer(mBufferInfo, 10000);
            if (encoderStatus == MediaCodec.INFO_TRY_AGAIN_LATER) {
                if (!endOfStream) break;
            } else if (encoderStatus == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                MediaFormat newFormat = mEncoder.getOutputFormat();
                mTrackIndex = mMuxer.addTrack(newFormat);
                mMuxer.start();
                mMuxerStarted = true;
            } else if (encoderStatus >= 0) {
                ByteBuffer encodedData = mEncoder.getOutputBuffer(encoderStatus);
                if ((mBufferInfo.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) {
                    mBufferInfo.size = 0;
                }
                if (mBufferInfo.size != 0 && mMuxerStarted) {
                    encodedData.position(mBufferInfo.offset);
                    encodedData.limit(mBufferInfo.offset + mBufferInfo.size);
                    mMuxer.writeSampleData(mTrackIndex, encodedData, mBufferInfo);
                }
                mEncoder.releaseOutputBuffer(encoderStatus, false);
                if ((mBufferInfo.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) break;
            }
        }
    }

    public void release() {
        drainEncoder(true);
        try {
            if (mEncoder != null) {
                mEncoder.stop();
                mEncoder.release();
            }
            if (mMuxer != null) {
                if (mMuxerStarted) mMuxer.stop();
                mMuxer.release();
            }
        } catch (Exception e) {
            Log.e(TAG, "Release error: " + e.getMessage());
        }
    }

    private void encodeYUV420SP(byte[] yuv420sp, byte[] rgba, int width, int height) {
        final int frameSize = width * height;
        int yIndex = 0;
        int uvIndex = frameSize;

        for (int j = 0; j < height; j++) {
            for (int i = 0; i < width; i++) {
                int r = rgba[(j * width + i) * 4] & 0xff;
                int g = rgba[(j * width + i) * 4 + 1] & 0xff;
                int b = rgba[(j * width + i) * 4 + 2] & 0xff;

                // RGB to YUV formula
                int y = ((66 * r + 129 * g + 25 * b + 128) >> 8) + 16;
                int u = ((-38 * r - 74 * g + 112 * b + 128) >> 8) + 128;
                int v = ((112 * r - 94 * g - 18 * b + 128) >> 8) + 128;

                yuv420sp[yIndex++] = (byte) ((y < 0) ? 0 : ((y > 255) ? 255 : y));
                if (j % 2 == 0 && i % 2 == 0) {
                    yuv420sp[uvIndex++] = (byte) ((v < 0) ? 0 : ((v > 255) ? 255 : v));
                    yuv420sp[uvIndex++] = (byte) ((u < 0) ? 0 : ((u > 255) ? 255 : u));
                }
            }
        }
    }
}
