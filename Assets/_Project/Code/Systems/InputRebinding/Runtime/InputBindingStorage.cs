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
}
