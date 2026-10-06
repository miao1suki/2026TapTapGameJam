using Project.InputAbstraction;

namespace Project.InputRebinding
{
    public interface IInputBindingStorage
    {
        string Load();
        void Save(string json);
        void Clear();
    }

    public sealed class PlayerPrefsInputBindingStorage :
        IInputBindingStorage
    {
        public string Load()
        {
            return InputBindingOverrideStore.Load();
        }

        public void Save(string json)
        {
            InputBindingOverrideStore.Save(json);
        }

        public void Clear()
        {
            InputBindingOverrideStore.Clear();
        }
    }

    public sealed class MemoryInputBindingStorage :
        IInputBindingStorage
    {
        private string json = string.Empty;

        public string Load()
        {
            return json;
        }

        public void Save(string value)
        {
            json = value ?? string.Empty;
        }

        public void Clear()
        {
            json = string.Empty;
        }
    }
}
