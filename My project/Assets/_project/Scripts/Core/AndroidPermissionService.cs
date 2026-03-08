using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;

namespace Maptifier.Core
{
    public class AndroidPermissionService : IPermissionService
    {
        private readonly ICoroutineRunner _coroutineRunner;

        public AndroidPermissionService(ICoroutineRunner coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }

        public bool HasPermission(PermissionType type)
        {
            var androidPermissions = GetAndroidPermissions(type);
            foreach (var p in androidPermissions)
            {
                if (!Permission.HasUserAuthorizedPermission(p))
                    return false;
            }
            return true;
        }

        public void RequestPermission(PermissionType type, Action<PermissionStatus> onResult)
        {
            if (HasPermission(type))
            {
                onResult?.Invoke(PermissionStatus.Granted);
                return;
            }

            var androidPermissions = GetAndroidPermissions(type);
            _coroutineRunner.RunCoroutine(RequestRoutine(androidPermissions, onResult));
        }

        private IEnumerator RequestRoutine(string[] permissions, Action<PermissionStatus> onResult)
        {
            // Request one by one if multiple (though usually we just need the main one)
            foreach (var p in permissions)
            {
                var callbacks = new PermissionCallbacks();
                bool completed = false;
                PermissionStatus result = PermissionStatus.Denied;

                callbacks.PermissionGranted += (name) => { completed = true; result = PermissionStatus.Granted; };
                callbacks.PermissionDenied += (name) =>
                {
                    completed = true;
                    result = Permission.ShouldShowRequestPermissionRationale(p)
                        ? PermissionStatus.Denied
                        : PermissionStatus.DeniedAndDontAskAgain;
                };

                Permission.RequestUserPermission(p, callbacks);

                while (!completed)
                {
                    yield return null;
                }

                if (result != PermissionStatus.Granted)
                {
                    onResult?.Invoke(result);
                    yield break;
                }
            }

            onResult?.Invoke(PermissionStatus.Granted);
        }

        private string[] GetAndroidPermissions(PermissionType type)
        {
            switch (type)
            {
                case PermissionType.Storage:
                    // Android 13 (API 33) and above uses granular media permissions
                    if (GetSDKInt() >= 33)
                    {
                        return new[] {
                            "android.permission.READ_MEDIA_IMAGES",
                            "android.permission.READ_MEDIA_VIDEO",
                            "android.permission.READ_MEDIA_AUDIO"
                        };
                    }
                    return new[] { Permission.ExternalStorageRead, Permission.ExternalStorageWrite };

                case PermissionType.Camera:
                    return new[] { Permission.Camera };

                case PermissionType.Notifications:
                    if (GetSDKInt() >= 33)
                        return new[] { "android.permission.POST_NOTIFICATIONS" };
                    return Array.Empty<string>();

                default:
                    return Array.Empty<string>();
            }
        }

        private int GetSDKInt()
        {
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
        }
    }
}
