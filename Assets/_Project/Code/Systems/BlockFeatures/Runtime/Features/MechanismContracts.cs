using System;
using UnityEngine;

namespace Project.BlockFeatures
{
    public interface IEnergySource
    {
        bool IsEnergyAvailable { get; }
        bool CanMigrate { get; }
    }

    public interface IMechanismSignalSource
    {
        bool IsSignalActive { get; }
        event Action<bool> SignalChanged;
    }

    public interface IMechanismSignalReceiver
    {
        void SetSignal(bool active, Component source);
    }
}
