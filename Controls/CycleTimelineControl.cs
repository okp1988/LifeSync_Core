using System.Windows;
using System.Windows.Media;
using LifeSyncTaskClient.Models;

namespace LifeSyncTaskClient.Controls;

public sealed class CycleTimelineControl : FrameworkElement
{
    private const double BlockWidth = 9;
    private const double BlockHeight = 15;
    private const double BlockStep = 11;
    private const double FirstBlockOffset = 1;

    private static readonly Brush DefaultBrush = CreateBrush(0xCD, 0xEF, 0xD3);
    private static readonly Pen DefaultPen = CreatePen(0x8B, 0xAA, 0x91);
    private static readonly Brush TodayBrush = CreateBrush(0x11, 0x11, 0x11);
    private static readonly Pen TodayPen = CreatePen(0x11, 0x11, 0x11);
    private static readonly Brush WarningBrush = CreateBrush(0xB7, 0x79, 0x1F);
    private static readonly Pen WarningPen = CreatePen(0x74, 0x45, 0x0B);
    private static readonly Brush AfterWarningBrush = CreateBrush(0x24, 0x57, 0xA6);
    private static readonly Pen AfterWarningPen = CreatePen(0x17, 0x3B, 0x73);
    private static readonly Brush ExpiredBrush = CreateBrush(0xA6, 0x1B, 0x1B);
    private static readonly Pen ExpiredPen = CreatePen(0x70, 0x12, 0x12);
    private static readonly Brush GrayBrush = CreateBrush(0x68, 0x71, 0x80);
    private static readonly Pen GrayPen = CreatePen(0x44, 0x4B, 0x56);

    public static readonly DependencyProperty BlocksProperty = DependencyProperty.Register(
        nameof(Blocks),
        typeof(IReadOnlyList<CycleTimelineBlock>),
        typeof(CycleTimelineControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<CycleTimelineBlock>? Blocks
    {
        get => (IReadOnlyList<CycleTimelineBlock>?)GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (Blocks is null)
        {
            return;
        }

        var blockCount = Math.Min(10, Blocks.Count);
        for (var index = 0; index < blockCount; index++)
        {
            var (fill, border) = GetColors(Blocks[index].State);
            var bounds = new Rect(FirstBlockOffset + (index * BlockStep), 0, BlockWidth, BlockHeight);
            drawingContext.DrawRoundedRectangle(fill, border, bounds, 1, 1);
        }
    }

    private static (Brush Fill, Pen Border) GetColors(string state)
    {
        return state switch
        {
            TimelineBlockStates.Today => (TodayBrush, TodayPen),
            TimelineBlockStates.Warning => (WarningBrush, WarningPen),
            TimelineBlockStates.AfterWarning => (AfterWarningBrush, AfterWarningPen),
            TimelineBlockStates.Red => (ExpiredBrush, ExpiredPen),
            TimelineBlockStates.Gray => (GrayBrush, GrayPen),
            _ => (DefaultBrush, DefaultPen)
        };
    }

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(byte red, byte green, byte blue)
    {
        var pen = new Pen(CreateBrush(red, green, blue), 1);
        pen.Freeze();
        return pen;
    }
}
