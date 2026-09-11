using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkitDemo.Messages;
using CommunityToolkitDemo.Models;

namespace CommunityToolkitDemo.Services;

/// <summary>基于内存的设置服务实现。</summary>
public sealed class SettingsService : ISettingsService
{
    private readonly IMessenger _messenger;

    public SettingsService(IMessenger messenger)
    {
        _messenger = messenger;
        Current = PosSettings.Default;
    }

    public PosSettings Current { get; private set; }

    public void Save(PosSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Current = settings;
        _messenger.Send(new SettingsChangedMessage(settings));
    }

    public void Reset() => Save(PosSettings.Default);
}
