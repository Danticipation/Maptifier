using System;

namespace Maptifier.Core
{
    public enum PermissionStatus
    {
        NotRequested,
        Granted,
        Denied,
        DeniedAndDontAskAgain
    }

    public enum PermissionType
    {
        Storage,
        Camera,
        Notifications
    }

    public interface IPermissionService
    {
        bool HasPermission(PermissionType type);
        void RequestPermission(PermissionType type, Action<PermissionStatus> onResult);
    }
}
