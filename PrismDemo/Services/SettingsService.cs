using Prism.Events;
using PrismDemo.Events;
using PrismDemo.Models;

namespace PrismDemo.Services;

/// <summary>基于内存的设置服务实现。</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IEventAggregator _eventAggregator;

    public SettingsService(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        Current = PosSettings.Default;
    }

    public PosSettings Current { get; private set; }

    public void Save(PosSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Current = settings;
        _eventAggregator.GetEvent<SettingsChangedEvent>().Publish(settings);
    }

    public void Reset() => Save(PosSettings.Default);
}
