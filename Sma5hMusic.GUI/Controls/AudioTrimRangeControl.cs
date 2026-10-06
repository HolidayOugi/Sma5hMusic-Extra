using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace Sma5hMusic.GUI.Controls
{
    public class AudioTrimRangeControl : Control
    {
        //waveform rendering constants
        private const double TrackPadding = 8;
        private const double ThumbWidth = 1;
        private const double VerticalPadding = 6;
        private const double LoopMarkerAreaHeight = 10;
        private const double MaximumZoomFactor = 1000;

        private static readonly IBrush SelectionBrush = new SolidColorBrush(Color.Parse("#401E88E5"));
        private static readonly IBrush OutsideWaveformBrush = new SolidColorBrush(Color.Parse("#66808080"));
        private static readonly IBrush SelectedWaveformBrush = new SolidColorBrush(Color.Parse("#D9FFFFFF"));
        private static readonly IBrush HandleBrush = new SolidColorBrush(Color.Parse("#B0168CE0"));
        private static readonly Pen OutsideWaveformPen = new Pen(OutsideWaveformBrush, 1);
        private static readonly Pen SelectedWaveformPen = new Pen(SelectedWaveformBrush, 1.15);
        private static readonly Pen SelectionOutlinePen = new Pen(HandleBrush, 1.25);
        private static readonly Pen LoopMarkerPen = new Pen(new SolidColorBrush(Color.Parse("#FFFFB74D")), 2);

        private DraggedThumb _draggedThumb;

        public static readonly StyledProperty<uint> MinimumProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(nameof(Minimum));

        public static readonly StyledProperty<uint> MaximumProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(nameof(Maximum), 1);

        public static readonly StyledProperty<uint> StartValueProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(
                nameof(StartValue),
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<uint> EndValueProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(
                nameof(EndValue),
                1,
                defaultBindingMode: BindingMode.TwoWay);

        public static readonly StyledProperty<float[]> WaveformPeaksProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, float[]>(
                nameof(WaveformPeaks),
                Array.Empty<float>());

        public static readonly StyledProperty<bool> ShowLoopMarkersProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, bool>(nameof(ShowLoopMarkers));

        public static readonly StyledProperty<uint> LoopStartMarkerProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(nameof(LoopStartMarker));

        public static readonly StyledProperty<uint> LoopEndMarkerProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(nameof(LoopEndMarker));

        public static readonly StyledProperty<uint> ViewStartProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(nameof(ViewStart));

        public static readonly StyledProperty<uint> ViewLengthProperty =
            AvaloniaProperty.Register<AudioTrimRangeControl, uint>(nameof(ViewLength), 1);

        static AudioTrimRangeControl()
        {
            AffectsRender<AudioTrimRangeControl>(
                MinimumProperty,
                MaximumProperty,
                StartValueProperty,
                EndValueProperty,
                WaveformPeaksProperty,
                ShowLoopMarkersProperty,
                LoopStartMarkerProperty,
                LoopEndMarkerProperty,
                ViewStartProperty,
                ViewLengthProperty);
        }

        public uint Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public uint Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public uint StartValue
        {
            get => GetValue(StartValueProperty);
            set => SetValue(StartValueProperty, value);
        }

        public uint EndValue
        {
            get => GetValue(EndValueProperty);
            set => SetValue(EndValueProperty, value);
        }

        public float[] WaveformPeaks
        {
            get => GetValue(WaveformPeaksProperty);
            set => SetValue(WaveformPeaksProperty, value);
        }

        public bool ShowLoopMarkers
        {
            get => GetValue(ShowLoopMarkersProperty);
            set => SetValue(ShowLoopMarkersProperty, value);
        }

        public uint LoopStartMarker
        {
            get => GetValue(LoopStartMarkerProperty);
            set => SetValue(LoopStartMarkerProperty, value);
        }

        public uint LoopEndMarker
        {
            get => GetValue(LoopEndMarkerProperty);
            set => SetValue(LoopEndMarkerProperty, value);
        }

        public uint ViewStart
        {
            get => GetValue(ViewStartProperty);
            set => SetValue(ViewStartProperty, value);
        }

        public uint ViewLength
        {
            get => GetValue(ViewLengthProperty);
            set => SetValue(ViewLengthProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

            var trackLeft = TrackPadding;
            var trackWidth = Math.Max(1, Bounds.Width - (TrackPadding * 2));
            var startX = ValueToX(StartValue, trackLeft, trackWidth);
            var endX = ValueToX(EndValue, trackLeft, trackWidth);
            var waveformTop = VerticalPadding;
            var waveformHeight = Math.Max(1, Bounds.Height - (VerticalPadding * 2) - LoopMarkerAreaHeight);
            var selectionRect = new Rect(startX, waveformTop, Math.Max(0, endX - startX), waveformHeight);

            context.FillRectangle(SelectionBrush, selectionRect);
            DrawWaveform(context, OutsideWaveformPen, trackLeft, trackWidth, waveformTop, waveformHeight);

            using (context.PushClip(selectionRect))
                DrawWaveform(context, SelectedWaveformPen, trackLeft, trackWidth, waveformTop, waveformHeight);

            context.DrawLine(SelectionOutlinePen, new Point(startX, waveformTop), new Point(endX, waveformTop));
            context.DrawLine(SelectionOutlinePen, new Point(startX, waveformTop + waveformHeight), new Point(endX, waveformTop + waveformHeight));
            context.FillRectangle(
                HandleBrush,
                new Rect(startX - (ThumbWidth / 2), waveformTop, ThumbWidth, waveformHeight));
            context.FillRectangle(
                HandleBrush,
                new Rect(endX - (ThumbWidth / 2), waveformTop, ThumbWidth, waveformHeight));

            if (ShowLoopMarkers)
            {
                var markerTop = waveformTop + waveformHeight + 2;
                var markerBottom = Math.Min(Bounds.Height - 1, markerTop + 6);
                var loopStartX = ValueToX(LoopStartMarker, trackLeft, trackWidth);
                var loopEndX = ValueToX(LoopEndMarker, trackLeft, trackWidth);
                context.DrawLine(LoopMarkerPen, new Point(loopStartX, markerTop), new Point(loopStartX, markerBottom));
                context.DrawLine(LoopMarkerPen, new Point(loopEndX, markerTop), new Point(loopEndX, markerBottom));
            }
        }

        private void DrawWaveform(
            DrawingContext context,
            Pen pen,
            double left,
            double width,
            double top,
            double height)
        {
            var peaks = WaveformPeaks;
            var centerY = top + (height / 2);
            if (peaks == null || peaks.Length == 0)
            {
                context.DrawLine(pen, new Point(left, centerY), new Point(left + width, centerY));
                return;
            }

            var halfHeight = height / 2;
            var visibleStart = GetVisibleStart();
            var visibleLength = GetVisibleLength();
            var columnCount = Math.Max(1, (int)Math.Ceiling(width));
            for (var column = 0; column < columnCount; column++)
            {
                var startRatio = (visibleStart + (column / (double)columnCount * visibleLength)) / Maximum;
                var endRatio = (visibleStart + ((column + 1) / (double)columnCount * visibleLength)) / Maximum;
                var startIndex = Math.Max(0, Math.Min(peaks.Length - 1, (int)Math.Floor(startRatio * peaks.Length)));
                var endIndex = Math.Max(startIndex + 1, Math.Min(peaks.Length, (int)Math.Ceiling(endRatio * peaks.Length)));
                var peak = 0f;
                for (var index = startIndex; index < endIndex; index++)
                    peak = Math.Max(peak, peaks[index]);

                var x = left + (column / (double)Math.Max(1, columnCount - 1) * width);
                var amplitude = Math.Max(0, Math.Min(1, peak)) * halfHeight;
                context.DrawLine(
                    pen,
                    new Point(x, centerY - amplitude),
                    new Point(x, centerY + amplitude));
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var point = e.GetPosition(this);
            if (GetVisibleLength() < Maximum - Minimum)
            {
                _draggedThumb = point.X <= Bounds.Width / 2
                    ? DraggedThumb.Start
                    : DraggedThumb.End;
            }
            else
            {
                var value = XToValue(point.X);
                var startDistance = Math.Abs((long)value - StartValue);
                var endDistance = Math.Abs((long)value - EndValue);
                _draggedThumb = startDistance <= endDistance
                    ? DraggedThumb.Start
                    : DraggedThumb.End;
            }
            e.Pointer.Capture(this);
            UpdateValueFromPointer(point.X);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_draggedThumb == DraggedThumb.None)
                return;

            UpdateValueFromPointer(e.GetPosition(this).X);
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (_draggedThumb == DraggedThumb.None)
                return;

            UpdateValueFromPointer(e.GetPosition(this).X);
            _draggedThumb = DraggedThumb.None;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        //zoom functionality
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            if (e.Delta.Y == 0 || Maximum <= Minimum)
                return;

            var currentLength = GetVisibleLength();
            var rangeLength = Maximum - Minimum;
            var minimumLength = Math.Max(1, Math.Ceiling(rangeLength / MaximumZoomFactor));
            var nextLength = e.Delta.Y > 0
                ? (uint)Math.Max(minimumLength, Math.Floor(currentLength * 0.75))
                : (uint)Math.Min(rangeLength, Math.Ceiling(currentLength / 0.75));

            if (nextLength == currentLength)
            {
                e.Handled = true;
                return;
            }

            var point = e.GetPosition(this);
            var trackWidth = Math.Max(1, Bounds.Width - (TrackPadding * 2));
            var pointerRatio = Math.Max(0, Math.Min(1, (point.X - TrackPadding) / trackWidth));
            var pointerValue = GetVisibleStart() + pointerRatio * currentLength;
            var nextStart = pointerValue - pointerRatio * nextLength;
            var maxStart = Maximum - nextLength;

            SetValue(ViewLengthProperty, nextLength);
            SetValue(ViewStartProperty, (uint)Math.Max(Minimum, Math.Min(maxStart, Math.Round(nextStart))));
            e.Handled = true;
        }

        private void UpdateValueFromPointer(double x)
        {
            var value = XToValue(x);

            if (_draggedThumb == DraggedThumb.Start)
            {
                var latestStart = EndValue == 0 ? 0 : EndValue - 1;
                SetValue(StartValueProperty, Math.Min(value, latestStart));
            }
            else if (_draggedThumb == DraggedThumb.End)
            {
                var earliestEnd = StartValue == uint.MaxValue ? uint.MaxValue : StartValue + 1;
                SetValue(EndValueProperty, Math.Max(value, earliestEnd));
            }
        }

        private double ValueToX(uint value, double trackLeft, double trackWidth)
        {
            var minimum = GetVisibleStart();
            var maximum = GetVisibleEnd();
            if (maximum <= minimum)
                return trackLeft;

            var clamped = Math.Max(minimum, Math.Min(maximum, value));
            return trackLeft + ((clamped - minimum) / (double)(maximum - minimum) * trackWidth);
        }

        private uint XToValue(double x)
        {
            var minimum = GetVisibleStart();
            var maximum = GetVisibleEnd();
            var trackWidth = Math.Max(1, Bounds.Width - (TrackPadding * 2));
            var ratio = Math.Max(0, Math.Min(1, (x - TrackPadding) / trackWidth));
            return minimum + (uint)Math.Round(ratio * (maximum - minimum));
        }

        private uint GetVisibleStart()
        {
            var length = GetVisibleLength();
            return Math.Min(Math.Max(Minimum, ViewStart), Maximum - length);
        }

        private uint GetVisibleLength()
        {
            return Math.Max(1, Math.Min(Maximum - Minimum, ViewLength));
        }

        private uint GetVisibleEnd()
        {
            return GetVisibleStart() + GetVisibleLength();
        }

        private enum DraggedThumb
        {
            None,
            Start,
            End
        }
    }
}
