using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WpfProgressSpinner
{
    // TODO : Implementation of each state -> None, Completed, Paused, Error
    public enum ProgressState
    {
        None = 0,
        Indeterminate = 1,
        Normal = 2,
        Completed = 3,
        Paused = 4,
        Error = 5
    }

    [TemplatePart(Name = "PART_Track", Type = typeof(FrameworkElement))]
    [TemplatePart(Name = "PART_Indicator", Type = typeof(FrameworkElement))]
    public sealed class ProgressSpinner : RangeBase
    {

        #region Data

        private const string TrackTemplateName = "PART_Track";
        private const string IndicatorTemplateName = "PART_Indicator";
        private static ProgressState _defaultState = ProgressState.Indeterminate;

        //private RotateTransform _indicatorRotation;

        private AnimationTimeline _spinAnimation;
        private AnimationTimeline _rotateAnimation;

        private static PathGeometry _spinAnimationPath;

        private FrameworkElement _track; // Currently not needed.
        private FrameworkElement _indicator;

        #endregion Data

        private double ProgressRatio
        {
            get { return Maximum <= Minimum ? 1.0 : (Value - Minimum) / (Maximum - Minimum); }
        }

        private bool RunningTransitionAnimation
        {
            get { return _transitionAnimationRunning; }
        }


        static ProgressSpinner()
        {
            // Override dependency properties

            FocusableProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(false));
            
            // Set default to 100.0
            MaximumProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(100.0, (d, e) => {
                ((ProgressSpinner)d).UpdateIndicator();
            }));

            // Set default to 0.0
            MinimumProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(0.0, (d, e) => {
                ((ProgressSpinner)d).UpdateIndicator();
            }));

            // Set default size properties
            WidthProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(40.0));
            HeightProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(40.0));

            StyleProperty.OverrideMetadata(typeof(ProgressSpinner), new FrameworkPropertyMetadata(CreateStyle()));

            // Create a PathGeometry for PointAnimationgUsingPath
            // It is the base curve for the spinner arc animation calculations.
            _spinAnimationPath = PathGeometry.CreateFromGeometry(
                Geometry.Parse("M 0 0.1 L 0 0.65 C 0.001 0.898 0.126 0.927 0.247 0.954 C 0.394 0.988 0.43 0.995 0.61 1.038 C 0.719 1.062 0.846 1.101 1 1.1")
            );
            _spinAnimationPath.Freeze();
        }

        public ProgressSpinner()
        {
            var animationSpeedBinding = new Binding("AnimationSpeedRatio")
            {
                Source = this,
                Mode = BindingMode.OneWay,
            };

            // Animation of a circular bar stretching and shrinking and spinning
            _spinAnimation = new PointAnimationUsingPath
            {
                PathGeometry = _spinAnimationPath,
                Duration = TimeSpan.FromSeconds(1.35),
                AccelerationRatio = 0.2,
                DecelerationRatio = 0.4,
            };

            //Timeline.SetDesiredFrameRate(_spinAnimation, 60);
            BindingOperations.SetBinding(_spinAnimation, Timeline.SpeedRatioProperty, animationSpeedBinding);
            _spinAnimation.Completed += SpinAnimation_Completed;

            // RotateTransform Animation
            _rotateAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(3),
            };
            
            //Timeline.SetDesiredFrameRate(_rotateAnimation, 60);
            BindingOperations.SetBinding(_rotateAnimation, Timeline.SpeedRatioProperty, animationSpeedBinding);
            _rotateAnimation.Completed += RotateAnimation_Completed;

            Loaded += (s, e) => {
                UpdateIndicator();
                UpdateAnimation();
            };

            IsVisibleChanged += (s, e) =>
            {
                if ((bool)e.NewValue)
                {
                    UpdateIndicator();
                    UpdateAnimation();
                }
                /*else
                {
                    StopAnimation();
                }*/
            };
        }

        private void UpdateIndicator()
        {
            double startPointRatio;
            double endPointRatio;
            if (IsIndeterminate || RunningTransitionAnimation)
            {
                startPointRatio = IndicatorAnimator.X;
                endPointRatio = IndicatorAnimator.Y;

                if (startPointRatio > endPointRatio)
                {
                    startPointRatio = endPointRatio;
                }
            }
            else
            {
                startPointRatio = 0;
                endPointRatio = ProgressRatio;
            }

            SetCurrentValue(IndicatorCompositorProperty, new Point(startPointRatio, endPointRatio));
        }


        private bool _indeterminateAnimationRunning = false;
        private bool _transitionAnimationRunning = false;
        private void UpdateAnimation()
        {
            if (IsIndeterminate)
            {
                if (IsVisible && !_indeterminateAnimationRunning) {
                    var currentRatio = ProgressRatio;
                    var duration = TimeSpan.FromSeconds((0.625 * Math.Abs(currentRatio - 1.1)) + 0.8);

                    // Create a transition, based on the current value
                    PathGeometry enterAnimationPath = new PathGeometry {
                        Figures = new PathFigureCollection(1)
                        {
                            new PathFigure
                            {
                                StartPoint = new Point(0, currentRatio),
                                Segments = new PathSegmentCollection(1)
                                {
                                    new PolyBezierSegment
                                    {
                                        Points = new PointCollection(3)
                                        {
                                            new Point(0, 1.1),
                                            new Point(0, 1.1),
                                            new Point(1, 1.1)
                                        }
                                    }
                                }
                            }
                        }
                    };
                    enterAnimationPath.Freeze();

                    PointAnimationUsingPath enterSpinAnimation = new PointAnimationUsingPath
                    {
                        PathGeometry = enterAnimationPath,
                        Duration = duration,
                        AccelerationRatio = 0.4,
                        DecelerationRatio = 0.4,
                        SpeedRatio = AnimationSpeedRatio
                    };

                    enterSpinAnimation.Completed += SpinAnimation_Completed;
                    enterSpinAnimation.Freeze();
                    BeginAnimation(IndeterminateAnimatorProperty, enterSpinAnimation, HandoffBehavior.SnapshotAndReplace);

                    // Create a transition to a continuous rotate animation
                    var enterRotateAnimation = new DoubleAnimation
                    {
                        To = 360,
                        Duration = TimeSpan.FromSeconds(3 * Math.Abs((IndicatorRotate - 360) / 360) + 0.5),
                        EasingFunction = new PowerEase { EasingMode = EasingMode.EaseIn },
                        SpeedRatio = AnimationSpeedRatio
                    };
                    enterRotateAnimation.Completed += RotateAnimation_Completed;
                    BeginAnimation(IndicatorRotateProperty, enterRotateAnimation);

                    _transitionAnimationRunning = false;
                    _indeterminateAnimationRunning = true;
                }
            }
            else if (_indeterminateAnimationRunning)
            {
                _indeterminateAnimationRunning = false;
                _transitionAnimationRunning = true;

                // Create a transition to animate RotateTransform to initial value
                var currentAngle = IndicatorRotate - 360;
                var exitRotateAnimation = new DoubleAnimation
                {
                    From = currentAngle,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(currentAngle * -0.006),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
                    SpeedRatio = AnimationSpeedRatio
                };
                exitRotateAnimation.Freeze();
                BeginAnimation(IndicatorRotateProperty, exitRotateAnimation, HandoffBehavior.SnapshotAndReplace);
            }
        }

        private void StopAnimation()
        {
            _indeterminateAnimationRunning = false;
            _transitionAnimationRunning = false;
            BeginAnimation(IndicatorRotateProperty, null, HandoffBehavior.SnapshotAndReplace);
            BeginAnimation(IndeterminateAnimatorProperty, null, HandoffBehavior.SnapshotAndReplace);
        }

        private void RotateAnimation_Completed(object sender, EventArgs e)
        {
            if (!IsVisible)
            {
                StopAnimation();
            }
            else if (_indeterminateAnimationRunning)
                BeginAnimation(IndicatorRotateProperty, _rotateAnimation, HandoffBehavior.SnapshotAndReplace);
        }

        private void SpinAnimation_Completed(object sender, EventArgs e)
        {
            if (!IsVisible)
            {
                StopAnimation();
            }
            else if (_indeterminateAnimationRunning)
            {
                // Continue the spin animation
                BeginAnimation(IndeterminateAnimatorProperty, _spinAnimation, HandoffBehavior.SnapshotAndReplace);
            }
            else
            {
                _transitionAnimationRunning = true;
                var targetRatio = ProgressRatio;

                // Create a transition to animate the indicator to current value
                AnimationTimeline exitSpinAnimation;
                if (targetRatio < 0.1) // If ProgressRatio is less than 0.1, one additional spin animation is performed
                {
                    PathGeometry exitSpinAnimationPath = new PathGeometry
                    {
                        Figures = new PathFigureCollection(1)
                        {
                            new PathFigure
                            {
                                StartPoint = new Point(0, 0.1),
                                Segments = new PathSegmentCollection(1)
                                {
                                    new PolyQuadraticBezierSegment
                                    {
                                        Points = new PointCollection(4)
                                        {
                                            new Point(0, targetRatio + 0.8),
                                            new Point(0.1, targetRatio + 0.85),
                                            new Point(0.9, targetRatio + 0.95),
                                            new Point(1.0, targetRatio + 1.0)
                                        }
                                    }
                                }
                            }
                        }
                    };
                    exitSpinAnimationPath.Freeze();
                    exitSpinAnimation = new PointAnimationUsingPath
                    {
                        PathGeometry = exitSpinAnimationPath,
                        Duration = TimeSpan.FromSeconds(1.35),
                        AccelerationRatio = 0.2,
                        DecelerationRatio = 0.4,
                        SpeedRatio = AnimationSpeedRatio,
                    };
                }
                else
                {
                    exitSpinAnimation = new PointAnimation
                    {
                        From = new Point(0, 0.1),
                        To = new Point(0, targetRatio),
                        Duration = TimeSpan.FromSeconds(Math.Abs(targetRatio - 0.1) + 0.35),
                        AccelerationRatio = 0.1,
                        DecelerationRatio = 0.4,
                        SpeedRatio = AnimationSpeedRatio,
                    };
                }

                exitSpinAnimation.Completed += (s, _) => {
                    if (!IsIndeterminate)
                    {
                        _transitionAnimationRunning = false;
                        if (Value != SmoothValue)
                            BeginSmoothValueAnimation(SmoothValue);
                    }
                };
                exitSpinAnimation.Freeze();
                BeginAnimation(IndeterminateAnimatorProperty, exitSpinAnimation, HandoffBehavior.SnapshotAndReplace);
            }
        }

        private void BeginSmoothValueAnimation(double value)
        {
            if (!RunningTransitionAnimation)
            {
                if (IsIndeterminate)
                {
                    SetCurrentValue(ValueProperty, value);
                }
                else
                {
                    var valueAnimation = new DoubleAnimation
                    {
                        To = value,
                        Duration = TimeSpan.FromMilliseconds(500),
                    };
                    BeginAnimation(ValueProperty, valueAnimation, HandoffBehavior.SnapshotAndReplace);
                }
            }
        }

        private static void ProgressStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ProgressSpinner source = (ProgressSpinner)d;

            // Set [IsIndeterminate] dependency property
            source.SetValue(IsIndeterminatePropertyKey, (ProgressState)e.NewValue == ProgressState.Indeterminate);

            // Switch the required animation state according to changes in [ProgressState]
            source.UpdateAnimation();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            SetCurrentValue(MinHeightProperty, sizeInfo.NewSize.Width);
            UpdateIndicator();
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            base.OnValueChanged(oldValue, newValue);
            
            if (newValue == 100.0)
            {
                SetCurrentValue(ProgressStateProperty, ProgressState.Completed);
            }

            UpdateIndicator();
        }

        private static Style CreateStyle()
        {
            var indicator = new FrameworkElementFactory(typeof(DrawingVisualHost), IndicatorTemplateName);

            var ellipse = new FrameworkElementFactory(typeof(Ellipse), "ellipse");
            ellipse.SetValue(Shape.StrokeThicknessProperty, new TemplateBindingExtension(CircleThicknessProperty));
            ellipse.SetValue(Shape.StrokeProperty, new TemplateBindingExtension(BackgroundProperty));
            ellipse.SetValue(WidthProperty, new TemplateBindingExtension(ActualWidthProperty));
            ellipse.SetValue(HeightProperty, new TemplateBindingExtension(ActualWidthProperty));

            var canvas = new FrameworkElementFactory(typeof(Canvas), TrackTemplateName);
            canvas.AppendChild(ellipse);
            canvas.AppendChild(indicator);

            var root = new FrameworkElementFactory(typeof(Grid), "TemplateRoot");
            root.AppendChild(canvas);

            var ct = new ControlTemplate(typeof(RangeBase)) { 
                VisualTree = root
            };

            var style = new Style(typeof(RangeBase));
            style.Setters.Add(new Setter(TemplateProperty, ct));
            return style;
        }

        // Create a host visual derived from the FrameworkElement class.
        // This class provides only drawing geometry function.
        private sealed class DrawingVisualHost : FrameworkElement
        {
            public static readonly DependencyProperty GeometryProperty =
                DependencyProperty.Register("Geometry", typeof(Geometry), typeof(DrawingVisualHost),
                    new FrameworkPropertyMetadata(new StreamGeometry(), (d, e) =>
                    {
                        ((DrawingVisualHost)d).Render();
                    }));

            public static readonly DependencyProperty PenProperty =
                DependencyProperty.Register("Pen", typeof(Pen), typeof(DrawingVisualHost),
                    new FrameworkPropertyMetadata(new Pen(), (d, e) =>
                    {
                        ((DrawingVisualHost)d).Render();
                    }));

            private DrawingVisual _drawing;
            private VisualCollection _children;

            public DrawingVisualHost()
            {
                _children = new VisualCollection(this) { 
                    Capacity = 1,
                };

                _drawing = new DrawingVisual();
                _children.Add(_drawing);
            }

            private void Render()
            {
                using (DrawingContext context = _drawing.RenderOpen())
                {
                    context.DrawGeometry(null, (Pen)GetValue(PenProperty), (Geometry)GetValue(GeometryProperty));
                }
            }

            protected override int VisualChildrenCount
            {
                get { return _children.Count; }
            }

            protected override Visual GetVisualChild(int index)
            {
                if (index < 0 || index >= _children.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }

                return _children[index];
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Apply some bindings to [PART_Indicator]
            var indicator = GetTemplateChild(IndicatorTemplateName);
            if (indicator != null)
            {
                _indicator = (FrameworkElement)indicator;

                var indicatorData = new MultiBinding
                {
                    Converter = IndicatorConverter.I
                };
                indicatorData.Bindings.Add(new Binding("ActualWidth") { Source = this, Mode = BindingMode.OneWay });
                indicatorData.Bindings.Add(new Binding("CircleThickness") { Source = this, Mode = BindingMode.OneWay });
                indicatorData.Bindings.Add(new Binding("IndicatorCompositor") { Source = this, Mode = BindingMode.OneWay });
                indicatorData.Bindings.Add(new Binding("IndicatorRotate") { Source = this, Mode = BindingMode.OneWay });

                var indicatorPen = new MultiBinding
                {
                    Converter = PenConverter.I
                };
                indicatorPen.Bindings.Add(new Binding("CircleThickness") { Source = this, Mode = BindingMode.OneWay });
                indicatorPen.Bindings.Add(new Binding("Foreground") { Source = this, Mode = BindingMode.OneWay });

                _indicator.InvalidateProperty(DrawingVisualHost.GeometryProperty);
                _indicator.SetBinding(DrawingVisualHost.GeometryProperty, indicatorData);

                _indicator.InvalidateProperty(DrawingVisualHost.PenProperty);
                _indicator.SetBinding(DrawingVisualHost.PenProperty, indicatorPen);
            }
        }

        #region Dependency properties

        public ProgressState ProgressState
        {
            get { return (ProgressState)GetValue(ProgressStateProperty); }
            set { SetValue(ProgressStateProperty, value); }
        }
        public static readonly DependencyProperty ProgressStateProperty =
            DependencyProperty.Register("ProgressState",
                typeof(ProgressState), typeof(ProgressSpinner), new FrameworkPropertyMetadata(
                    _defaultState, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                        new PropertyChangedCallback(ProgressStateChanged)));

        public bool IsIndeterminate
        {
            get { return (bool)GetValue(IsIndeterminatePropertyKey.DependencyProperty); }
        }

        private static readonly DependencyPropertyKey IsIndeterminatePropertyKey =
            DependencyProperty.RegisterReadOnly("IsIndeterminate", typeof(bool),
                    typeof(ProgressSpinner), new FrameworkPropertyMetadata(_defaultState == ProgressState.Indeterminate));

        public double AnimationSpeedRatio
        {
            get { return (double)GetValue(AnimationSpeedRatioProperty); }
            set { SetValue(ProgressStateProperty, value); }
        }

        public static readonly DependencyProperty AnimationSpeedRatioProperty =
            DependencyProperty.Register("AnimationSpeedRatio",
                typeof(double), typeof(ProgressSpinner), new FrameworkPropertyMetadata(
                    1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, null, coerceValueCallback: (d, baseValue) =>
                    {
                        if ((double)baseValue > 0.1)
                            return baseValue;
                        else
                            return 0.1;
                    }));

        public double CircleThickness
        {
            get { return (double)GetValue(CircleThicknessProperty); }
            set { SetValue(CircleThicknessProperty, value); }
        }

        public static readonly DependencyProperty CircleThicknessProperty =
            DependencyProperty.Register("CircleThickness", typeof(double), typeof(ProgressSpinner),
                new FrameworkPropertyMetadata(5.0,
                    propertyChangedCallback:  (d, e) =>
                    {
                        ((ProgressSpinner)d).UpdateIndicator();
                    },
                    coerceValueCallback: (d, baseValue) =>
                    {
                        if ((double)baseValue > 0.0)
                            return baseValue;
                        else
                            return 0.0;
                    }
                )
            );

        public double SmoothValue
        {
            get { return (double)GetValue(SmoothValueProperty); }
            set { SetValue(SmoothValueProperty, value); }
        }

        public static readonly DependencyProperty SmoothValueProperty =
            DependencyProperty.Register("SmoothValue",
                typeof(double), typeof(ProgressSpinner), new PropertyMetadata(
                    (d, e) => { ((ProgressSpinner)d).BeginSmoothValueAnimation((double)e.NewValue); }));

        /*** Private dependency properties ***/

        private double IndicatorRotate
        {
            get { return (double)GetValue(IndicatorRotateProperty); }
            set { SetValue(IndicatorRotateProperty, value); }
        }

        private static readonly DependencyProperty IndicatorRotateProperty =
            DependencyProperty.Register("IndicatorRotate", typeof(double), typeof(ProgressSpinner),
                new FrameworkPropertyMetadata(0.0));

        private static readonly DependencyProperty IndicatorCompositorProperty =
            DependencyProperty.Register("IndicatorCompositor", typeof(Point), typeof(ProgressSpinner),
                new FrameworkPropertyMetadata(new Point()));

        private Point IndicatorAnimator
        {
            get { return (Point)GetValue(IndeterminateAnimatorProperty); }
            set { SetValue(IndeterminateAnimatorProperty, value); }
        }

        private static readonly DependencyProperty IndeterminateAnimatorProperty =
            DependencyProperty.Register("IndicatorAnimator", typeof(Point), typeof(ProgressSpinner),
                new FrameworkPropertyMetadata(new Point(), (d, e) =>
                {
                    ((ProgressSpinner)d).UpdateIndicator();
                }));

        #endregion Dependency properties



        #region Converters

        private sealed class IndicatorConverter : IMultiValueConverter
        {
            public static IndicatorConverter I = new IndicatorConverter();
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                double actualWidth = (double)values[0];
                double strokeThickness = (double)values[1];

                double startAngleRatio = ((Point)values[2]).X;
                double endAngleRatio = ((Point)values[2]).Y;

                double rotateOffset = (double)values[3];

                double radius;
                if (actualWidth > strokeThickness)
                {
                    radius = (actualWidth - strokeThickness) / 2.0;
                }
                else
                {
                    radius = 0.0;
                }

                double positionOffset = actualWidth / 2;

                double offsetRadians = rotateOffset / 360 * Math.PI * 2;
                double startRadians = startAngleRatio * Math.PI * 2 + offsetRadians;
                double endRadians = endAngleRatio * Math.PI * 2 + offsetRadians;

                double trisectRadians = (endRadians - startRadians) / 3;
                double mid1Radians = trisectRadians + startRadians;
                double mid2Radians = 2 * trisectRadians + startRadians;

                double startX = radius * Math.Sin(startRadians) + positionOffset;
                double startY = -radius * Math.Cos(startRadians) + positionOffset;

                double mid1X = radius * Math.Sin(mid1Radians) + positionOffset;
                double mid1Y = -radius * Math.Cos(mid1Radians) + positionOffset;

                double mid2X = radius * Math.Sin(mid2Radians) + positionOffset;
                double mid2Y = -radius * Math.Cos(mid2Radians) + positionOffset;

                double endX = radius * Math.Sin(endRadians) + positionOffset;
                double endY = -radius * Math.Cos(endRadians) + positionOffset;

                StreamGeometry geometry = new StreamGeometry();
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(startX, startY), false, false);
                    ctx.ArcTo(new Point(mid1X, mid1Y), new Size(radius, radius) ,0.0 , false, SweepDirection.Clockwise, true, false);
                    ctx.ArcTo(new Point(mid2X, mid2Y), new Size(radius, radius), 0.0, false, SweepDirection.Clockwise, true, false);
                    ctx.ArcTo(new Point(endX, endY), new Size(radius, radius), 0.0, false, SweepDirection.Clockwise, true, false);
                }
                geometry.Freeze();
                return geometry;
            }

            public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        private sealed class PenConverter : IMultiValueConverter
        {
            public static PenConverter I = new PenConverter();
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                double strokeThickness = (double)values[0];
                Brush brush = (Brush)values[1];
                var pen = new Pen
                {
                    Brush = brush,
                    Thickness = strokeThickness,
                    EndLineCap = PenLineCap.Round,
                    StartLineCap = PenLineCap.Round,
                };
                pen.Freeze();
                return pen;
            }

            public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        #endregion Converters
    }
}
