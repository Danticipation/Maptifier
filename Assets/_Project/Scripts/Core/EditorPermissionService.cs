using System;

namespace Maptifier.Core
{
    public class EditorPermissionService : IPermissionService
    {
        public bool HasPermission(PermissionType type) => true;

        public void RequestPermission(PermissionType type, Action<PermissionStatus> onResult)
        {
            onResult?.Invoke(PermissionStatus.Granted);
        }
    }
}
