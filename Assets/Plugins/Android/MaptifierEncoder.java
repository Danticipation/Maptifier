package com.maptifier.core;

import android.media.MediaCodec;
import android.media.MediaCodecInfo;
import android.media.MediaFormat;
import android.media.MediaMuxer;
import android.view.Surface;
import android.util.Log;
import java.io.IOException;
import java.nio.ByteBuffer;

/**
 * Native Android video encoder using MediaCodec and MediaMuxer.
 * Provides a Surface for Unity to render into via Graphics.Blit.
 */
public class MaptifierEncoder {
    private static final String TAG = "MaptifierEncoder";
    private static final String MIME_TYPE = "video/avc"; // H.264
    private static final int IFRAME_INTERVAL = 2; // 2 seconds between I-frames

    private MediaCodec mEncoder;
    private Surface mInputSurface;
    private MediaMuxer mMuxer;
    private int mTrackIndex;
    private boolean mMuxerStarted;
    private MediaCodec.BufferInfo mBufferInfo;
    private int mWidth;
    private int mHeight;

    public Surface init(String outputPath, int width, int height, int bitRate, int frameRate) throws IOException {
        Log.d(TAG, "Initializing encoder: " + width + "x" + height + " @ " + bitRate + "bps, " + frameRate + "fps");
        
        mWidth = width;
        mHeight = height;
        mBufferInfo = new MediaCodec.BufferInfo();

        MediaFormat format = MediaFormat.createVideoFormat(MIME_TYPE, width, height);
        format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface);
        format.setInteger(MediaFormat.KEY_BIT_RATE, bitRate);
        format.setInteger(MediaFormat.KEY_FRAME_RATE, frameRate);
        format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, IFRAME_INTERVAL);

        mEncoder = MediaCodec.createEncoderByType(MIME_TYPE);
        mEncoder.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE);
        mInputSurface = mEncoder.createInputSurface();
        mEncoder.start();

        mMuxer = new MediaMuxer(outputPath, MediaMuxer.OutputFormat.MUXER_OUTPUT_MPEG_4);
        mTrackIndex = -1;
        mMuxerStarted = false;

        return mInputSurface;
    }

    /**
     * Drains the encoder's output buffers and writes them to the muxer.
     * Should be called after every frame is rendered to the surface.
     */
    public void drainEncoder(boolean endOfStream) {
        if (endOfStream) {
            Log.d(TAG, "Signaling end of stream");
            mEncoder.signalEndOfStream();
        }

        while (true) {
            int encoderStatus = mEncoder.dequeueOutputBuffer(mBufferInfo, 10000);
            if (encoderStatus == MediaCodec.INFO_TRY_AGAIN_LATER) {
                if (!endOfStream) break; // out of buffers, but not done yet
            } else if (encoderStatus == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                if (mMuxerStarted) {
                    throw new RuntimeException("Format changed twice");
                }
                MediaFormat newFormat = mEncoder.getOutputFormat();
                Log.d(TAG, "Encoder output format changed: " + newFormat);
                mTrackIndex = mMuxer.addTrack(newFormat);
                mMuxer.start();
                mMuxerStarted = true;
            } else if (encoderStatus < 0) {
                Log.w(TAG, "Unexpected result from dequeueOutputBuffer: " + encoderStatus);
            } else {
                ByteBuffer encodedData = mEncoder.getOutputBuffer(encoderStatus);
                if (encodedData == null) {
                    throw new RuntimeException("getOutputBuffer returned null");
                }

                if ((mBufferInfo.flags & MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0) {
                    // Codec config data, ignore for muxing
                    mBufferInfo.size = 0;
                }

                if (mBufferInfo.size != 0) {
                    if (!mMuxerStarted) {
                        throw new RuntimeException("Muxer not started");
                    }
                    encodedData.position(mBufferInfo.offset);
                    encodedData.limit(mBufferInfo.offset + mBufferInfo.size);
                    mMuxer.writeSampleData(mTrackIndex, encodedData, mBufferInfo);
                }

                mEncoder.releaseOutputBuffer(encoderStatus, false);

                if ((mBufferInfo.flags & MediaCodec.BUFFER_FLAG_END_OF_STREAM) != 0) {
                    if (!endOfStream) {
                        Log.w(TAG, "Reached end of stream unexpectedly");
                    } else {
                        Log.d(TAG, "End of stream reached");
                    }
                    break;
                }
            }
        }
    }

    public void release() {
        Log.d(TAG, "Releasing encoder resources");
        try {
            if (mEncoder != null) {
                mEncoder.stop();
                mEncoder.release();
                mEncoder = null;
            }
            if (mInputSurface != null) {
                mInputSurface.release();
                mInputSurface = null;
            }
            if (mMuxer != null) {
                if (mMuxerStarted) {
                    mMuxer.stop();
                }
                mMuxer.release();
                mMuxer = null;
            }
        } catch (Exception e) {
            Log.e(TAG, "Error during release: " + e.getMessage());
        }
    }
}
