using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Project.GameFlow
{
    public interface IGameSystemService
    {
        int InitializationOrder { get; }
        bool IsInitialized { get; }
        IEnumerator Initialize();
    }

    [DisallowMultipleComponent]
    public sealed class GameSystemSceneRoot : MonoBehaviour
    {
        [SerializeField] private GameSystemSceneKind kind;
        [SerializeField, HideInInspector] private bool initialized;

        public GameSystemSceneKind Kind => kind;
        public bool IsInitialized => initialized;

        public void Configure(GameSystemSceneKind valueKind)
        {
            kind = valueKind;
        }

        public IEnumerator InitializeServices()
        {
            if (initialized)
            {
                yield break;
            }

            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            List<IGameSystemService> services = new List<IGameSystemService>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is IGameSystemService service)
                {
                    services.Add(service);
                }
            }

            services.Sort((left, right) =>
                left.InitializationOrder.CompareTo(right.InitializationOrder));
            for (int index = 0; index < services.Count; index++)
            {
                if (!services[index].IsInitialized)
                {
                    yield return services[index].Initialize();
                }
            }

            initialized = true;
        }
    }

}
