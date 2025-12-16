using UnityEngine;

namespace AiToolbox {
public static partial class Dalle {
    private class DalleContainer : MonoBehaviour {
        private static DalleContainer _instance;
        internal static DalleContainer instance {
            get {
                if (_instance == null) {
                    var container = new GameObject("DALL-E Container");
                    DontDestroyOnLoad(container);
                    container.hideFlags = HideFlags.HideInHierarchy;
                    _instance = container.AddComponent<DalleContainer>();
                }

                return _instance;
            }
        }

        private void OnApplicationQuit() {
            CancelAllRequests();
        }
    }
}
}