using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Images.Controls;

/// <summary>
/// Fixed-cell wrapping panel that realizes only the rows intersecting the vertical viewport.
/// WPF ships no virtualizing equivalent of <see cref="WrapPanel"/>; gallery tiles have a known
/// stride, so row-based recycling keeps layout deterministic while bounding thumbnail work.
/// </summary>
public sealed class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(190d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        IsPositiveFinite);

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight),
        typeof(double),
        typeof(VirtualizingWrapPanel),
        new FrameworkPropertyMetadata(190d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        IsPositiveFinite);

    private Size _extent;
    private Size _viewport;
    private Point _offset;
    private int _itemsPerRow = 1;

    public double ItemWidth
    {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    internal int RealizedItemCount => InternalChildren.Count;

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        var itemCount = owner?.Items.Count ?? 0;
        var width = ResolveViewportWidth(availableSize.Width);
        var height = ResolveViewportHeight(availableSize.Height);

        _itemsPerRow = Math.Max(1, (int)Math.Floor(width / ItemWidth));
        var rowCount = itemCount == 0 ? 0 : (int)Math.Ceiling(itemCount / (double)_itemsPerRow);
        UpdateScrollInfo(new Size(width, rowCount * ItemHeight), new Size(width, height));

        if (itemCount == 0)
        {
            CleanupItems(0, -1);
            return new Size(width, height);
        }

        var firstVisibleRow = Math.Max(0, (int)Math.Floor(VerticalOffset / ItemHeight));
        var visibleRows = Math.Max(1, (int)Math.Ceiling(height / ItemHeight));
        const int cacheRows = 1;
        var firstIndex = Math.Max(0, (firstVisibleRow - cacheRows) * _itemsPerRow);
        var lastIndex = Math.Min(
            itemCount - 1,
            (firstVisibleRow + visibleRows + cacheRows) * _itemsPerRow - 1);

        RealizeItems(owner!, firstIndex, lastIndex);
        CleanupItems(firstIndex, lastIndex);

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null) return finalSize;

        foreach (UIElement child in InternalChildren)
        {
            var itemIndex = owner.ItemContainerGenerator.IndexFromContainer(child);
            if (itemIndex < 0) continue;

            var row = itemIndex / _itemsPerRow;
            var column = itemIndex % _itemsPerRow;
            child.Arrange(new Rect(
                column * ItemWidth,
                row * ItemHeight - VerticalOffset,
                ItemWidth,
                ItemHeight));
        }

        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender, args);
        InvalidateMeasure();
    }

    protected override void BringIndexIntoView(int index)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null || index < 0 || index >= owner.Items.Count) return;

        var rowTop = (index / _itemsPerRow) * ItemHeight;
        var rowBottom = rowTop + ItemHeight;
        if (rowTop < VerticalOffset)
            SetVerticalOffset(rowTop);
        else if (rowBottom > VerticalOffset + ViewportHeight)
            SetVerticalOffset(rowBottom - ViewportHeight);
    }

    private void RealizeItems(ItemsControl owner, int firstIndex, int lastIndex)
    {
        var generator = ItemContainerGenerator ??
                        ((IItemContainerGenerator)owner.ItemContainerGenerator)
                        .GetItemContainerGeneratorForPanel(this);
        if (generator is null)
        {
            // An ItemsPanelTemplate may be measured once before WPF connects its panel generator.
            // Loaded/layout invalidation will measure again with the generator attached.
            return;
        }

        var startPosition = generator.GeneratorPositionFromIndex(firstIndex);
        var childIndex = startPosition.Offset == 0 ? startPosition.Index : startPosition.Index + 1;

        using var generation = generator.StartAt(
            startPosition,
            GeneratorDirection.Forward,
            allowStartAtRealizedItem: true);

        for (var itemIndex = firstIndex; itemIndex <= lastIndex; itemIndex++, childIndex++)
        {
            if (generator.GenerateNext(out var newlyRealized) is not UIElement child)
                break;
            if (newlyRealized)
            {
                if (childIndex >= InternalChildren.Count)
                    AddInternalChild(child);
                else
                    InsertInternalChild(childIndex, child);
                generator.PrepareItemContainer(child);
            }

            child.Measure(new Size(ItemWidth, ItemHeight));
        }
    }

    private void CleanupItems(int firstIndex, int lastIndex)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null) return;

        for (var childIndex = InternalChildren.Count - 1; childIndex >= 0; childIndex--)
        {
            var child = InternalChildren[childIndex];
            var itemIndex = owner.ItemContainerGenerator.IndexFromContainer(child);
            if (itemIndex >= firstIndex && itemIndex <= lastIndex) continue;

            var position = new GeneratorPosition(childIndex, 0);
            if (ItemContainerGenerator is IRecyclingItemContainerGenerator recyclingGenerator)
                recyclingGenerator.Recycle(position, 1);
            else
                ItemContainerGenerator.Remove(position, 1);
            RemoveInternalChildRange(childIndex, 1);
        }
    }

    private void UpdateScrollInfo(Size extent, Size viewport)
    {
        var changed = !AreClose(_extent.Width, extent.Width) ||
                      !AreClose(_extent.Height, extent.Height) ||
                      !AreClose(_viewport.Width, viewport.Width) ||
                      !AreClose(_viewport.Height, viewport.Height);

        _extent = extent;
        _viewport = viewport;
        _offset.X = 0;
        _offset.Y = CoerceVerticalOffset(_offset.Y);

        if (changed)
            ScrollOwner?.InvalidateScrollInfo();
    }

    private double ResolveViewportWidth(double availableWidth)
    {
        if (double.IsFinite(availableWidth) && availableWidth > 0)
            return availableWidth;
        if (ScrollOwner?.ViewportWidth is > 0 and < double.PositiveInfinity)
            return ScrollOwner.ViewportWidth;
        return Math.Max(ItemWidth, ActualWidth);
    }

    private double ResolveViewportHeight(double availableHeight)
    {
        if (double.IsFinite(availableHeight) && availableHeight > 0)
            return availableHeight;
        if (ScrollOwner?.ViewportHeight is > 0 and < double.PositiveInfinity)
            return ScrollOwner.ViewportHeight;
        return Math.Max(ItemHeight, ActualHeight);
    }

    private double CoerceVerticalOffset(double offset)
        => Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));

    private static bool IsPositiveFinite(object value)
        => value is double number && double.IsFinite(number) && number > 0;

    private static bool AreClose(double left, double right)
        => Math.Abs(left - right) < 0.1;

    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public double ExtentWidth => _extent.Width;
    public double ExtentHeight => _extent.Height;
    public double ViewportWidth => _viewport.Width;
    public double ViewportHeight => _viewport.Height;
    public double HorizontalOffset => _offset.X;
    public double VerticalOffset => _offset.Y;
    public ScrollViewer? ScrollOwner { get; set; }

    public void LineUp() => SetVerticalOffset(VerticalOffset - ItemHeight);
    public void LineDown() => SetVerticalOffset(VerticalOffset + ItemHeight);
    public void LineLeft() { }
    public void LineRight() { }
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - ItemHeight * 3);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + ItemHeight * 3);
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void PageLeft() { }
    public void PageRight() { }
    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        var coerced = CoerceVerticalOffset(offset);
        if (AreClose(coerced, _offset.Y)) return;

        _offset.Y = coerced;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner is null) return rectangle;

        var container = ItemsControl.ContainerFromElement(owner, visual as DependencyObject);
        var index = container is null ? -1 : owner.ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0)
            BringIndexIntoView(index);
        return rectangle;
    }
}
