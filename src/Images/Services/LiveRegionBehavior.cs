using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Images.Services;

/// <summary>
/// Raises the WPF live-region automation event when a status TextBlock changes. Rapid changes
/// are coalesced at dispatcher background priority so progress updates remain polite.
/// </summary>
public static class LiveRegionBehavior
{
    public static readonly DependencyProperty AnnounceChangesProperty = DependencyProperty.RegisterAttached(
        "AnnounceChanges",
        typeof(bool),
        typeof(LiveRegionBehavior),
        new PropertyMetadata(false, OnAnnounceChangesChanged));

    private static readonly ConditionalWeakTable<TextBlock, TextChangeSubscription> Subscriptions = new();

    public static bool GetAnnounceChanges(DependencyObject element)
        => (bool)element.GetValue(AnnounceChangesProperty);

    public static void SetAnnounceChanges(DependencyObject element, bool value)
        => element.SetValue(AnnounceChangesProperty, value);

    private static void OnAnnounceChangesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not TextBlock textBlock)
            throw new InvalidOperationException("LiveRegionBehavior can only announce TextBlock changes.");

        if ((bool)args.NewValue)
        {
            Subscriptions.GetValue(textBlock, static element => new TextChangeSubscription(element)).Enable();
            return;
        }

        if (Subscriptions.TryGetValue(textBlock, out var subscription))
        {
            subscription.Disable();
            Subscriptions.Remove(textBlock);
        }
    }

    private sealed class TextChangeSubscription
    {
        private static readonly DependencyPropertyDescriptor TextDescriptor =
            DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock))
            ?? throw new InvalidOperationException("TextBlock.Text change notifications are unavailable.");

        private readonly TextBlock _textBlock;
        private DispatcherOperation? _pendingAnnouncement;
        private bool _enabled;
        private bool _observingText;

        public TextChangeSubscription(TextBlock textBlock) => _textBlock = textBlock;

        public void Enable()
        {
            if (_enabled)
                return;

            _enabled = true;
            _textBlock.Loaded += OnLoaded;
            _textBlock.Unloaded += OnUnloaded;
            _textBlock.IsVisibleChanged += OnIsVisibleChanged;
            if (_textBlock.IsLoaded)
                StartObservingText();
        }

        public void Disable()
        {
            if (!_enabled)
                return;

            _enabled = false;
            _textBlock.Loaded -= OnLoaded;
            _textBlock.Unloaded -= OnUnloaded;
            _textBlock.IsVisibleChanged -= OnIsVisibleChanged;
            StopObservingText();
            AbortPendingAnnouncement();
        }

        private void OnLoaded(object sender, RoutedEventArgs args) => StartObservingText();

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            StopObservingText();
            AbortPendingAnnouncement();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if ((bool)args.NewValue)
                QueueAnnouncement();
        }

        private void StartObservingText()
        {
            if (_observingText)
                return;

            TextDescriptor.AddValueChanged(_textBlock, OnTextChanged);
            _observingText = true;
        }

        private void StopObservingText()
        {
            if (!_observingText)
                return;

            TextDescriptor.RemoveValueChanged(_textBlock, OnTextChanged);
            _observingText = false;
        }

        private void OnTextChanged(object? sender, EventArgs args)
        {
            if (!_textBlock.IsLoaded || AutomationProperties.GetLiveSetting(_textBlock) == AutomationLiveSetting.Off)
                return;

            QueueAnnouncement();
        }

        private void QueueAnnouncement()
        {
            if (!_textBlock.IsLoaded || !_textBlock.IsVisible ||
                AutomationProperties.GetLiveSetting(_textBlock) == AutomationLiveSetting.Off)
                return;

            if (_pendingAnnouncement?.Status == DispatcherOperationStatus.Pending)
                return;

            _pendingAnnouncement = _textBlock.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(RaiseLiveRegionChanged));
        }

        private void RaiseLiveRegionChanged()
        {
            _pendingAnnouncement = null;
            if (!_enabled || !_textBlock.IsLoaded || string.IsNullOrWhiteSpace(_textBlock.Text))
                return;

            var peer = UIElementAutomationPeer.FromElement(_textBlock)
                       ?? UIElementAutomationPeer.CreatePeerForElement(_textBlock);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        private void AbortPendingAnnouncement()
        {
            if (_pendingAnnouncement?.Status == DispatcherOperationStatus.Pending)
                _pendingAnnouncement.Abort();
            _pendingAnnouncement = null;
        }
    }
}
