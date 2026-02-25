using System.Collections;

namespace Maptifier.Core
{
    /// <summary>
    /// Provides coroutine execution for services that need async operations.
    /// Registered by AppBootstrapper (MonoBehaviour) during initialization.
    /// </summary>
    public interface ICoroutineRunner
    {
        void RunCoroutine(IEnumerator coroutine);
    }
}
