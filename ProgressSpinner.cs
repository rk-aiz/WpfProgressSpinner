using System;
using System.Collections.Generic;
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

        private RotateTransform _indicatorRotation;

        private AnimationTimeline _spinAnimation;
        private AnimationTimeline _rotateAnimation;

        private static PathGeometry _spinAnimationPath;
        private PathGeometry _indicatorGeometry;

        private FrameworkElement _track; // Currently not needed.
        private FrameworkElement _indicator;

        private PathFigure _arcsFigure;
        private ArcSegment _arc0;
        private ArcSegment _arc1;
        private ArcSegment _arc2;

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
            // It is the base curve for the spinner arc calculations.
            _spinAnimationPath = PathGeometry.CreateFromGeometry(
                Geometry.Parse("M 0 0.1 L 0 0.65 C 0.001 0.898 0.126 0.927 0.247 0.954 C 0.394 0.988 0.43 0.995 0.61 1.038 C 0.719 1.062 0.846 1.101 1 1.1"));
            _spinAnimationPath.Freeze();
        }

        public ProgressSpinner()
        {
            // RotateTransform : rotate the entire progress circle
            _indicatorRotation = new RotateTransform();
            var rotateCenter = new Binding("ActualWidth")
            {
                Source = this,
                Converter = FactorConverter.I,
                ConverterParameter = 0.5,
                Mode = BindingMode.OneWay
            };
            BindingOperations.SetBinding(_indicatorRotation, RotateTransform.CenterXProperty, rotateCenter);
            BindingOperations.SetBinding(_indicatorRotation, RotateTransform.CenterYProperty, rotateCenter);

            // Radius of the arc of progress cirecle
            var arcRadius = new MultiBinding{ 
                Converter = ArcRadiusConverter.I,
            };
            arcRadius.Bindings.Add(new Binding("ActualWidth")
            {
                Source = this,
                Mode = BindingMode.OneWay
            });
            arcRadius.Bindings.Add(new Binding("CircleThickness")
            {
                Source = this,
                Mode = BindingMode.OneWay
            });

            // Arc parts for the circular bar
            _arc0 = new ArcSegment { SweepDirection = SweepDirection.Clockwise };
            BindingOperations.SetBinding(_arc0, ArcSegment.SizeProperty, arcRadius);

            _arc1 = new ArcSegment { SweepDirection = SweepDirection.Clockwise };
            BindingOperations.SetBinding(_arc1, ArcSegment.SizeProperty, arcRadius);

            _arc2 = new ArcSegment { SweepDirection = SweepDirection.Clockwise };
            BindingOperations.SetBinding(_arc2, ArcSegment.SizeProperty, arcRadius);

            _arcsFigure = new PathFigure();
            _arcsFigure.Segments.Add(_arc0);
            _arcsFigure.Segments.Add(_arc1);
            _arcsFigure.Segments.Add(_arc2);

            _indicatorGeometry = new PathGeometry();
            _indicatorGeometry.Figures.Add(_arcsFigure);
            _indicatorGeometry.Transform = _indicatorRotation;

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
            BindingOperations.SetBinding(_spinAnimation, Timeline.SpeedRatioProperty, animationSpeedBinding);
            _spinAnimation.Completed += SpinAnimation_Completed;

            // RotateTransform Animation
            _rotateAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(3),
            };
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
                else
                {
                    StopAnimation();
                }
            };
        }


        private bool _indeterminateAnimationRunning = false;
        private bool _transitionAnimationRunning = false;
        private void UpdateAnimation()
        {
            if (IsVisible && ActualWidth != 0 && !_indeterminateAnimationRunning && IsIndeterminate)
            {
                var currentRatio = ProgressRatio;
                var duration = TimeSpan.FromSeconds((0.625 * Math.Abs(currentRatio - 1.1)) + 0.8);

                // Create a transition, based on the current value
                PathGeometry enterAnimationPath = new PathGeometry{
                    Figures = new PathFigureCollection
                    {
                        new PathFigure
                        {
                            StartPoint = new Point(0, currentRatio),
                            Segments = new PathSegmentCollection
                            {
                                new PolyBezierSegment
                                {
                                    Points = new PointCollection
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
                    Duration = TimeSpan.FromSeconds(3 * Math.Abs((_indicatorRotation.Angle - 360) / 360) + 0.5),
                    EasingFunction = new PowerEase { EasingMode = EasingMode.EaseIn },
                    SpeedRatio = AnimationSpeedRatio
                };
                enterRotateAnimation.Completed += RotateAnimation_Completed;
                _indicatorRotation.BeginAnimation(RotateTransform.AngleProperty, enterRotateAnimation);

                _transitionAnimationRunning = false;
                _indeterminateAnimationRunning = true;
            }
            else if (_indeterminateAnimationRunning)
            {
                _indeterminateAnimationRunning = false;
                _transitionAnimationRunning = true;

                // Create a transition to animate RotateTransform to initial value
                var currentAngle = _indicatorRotation.Angle - 360;
                var exitRotateAnimation = new DoubleAnimation
                {
                    From = currentAngle,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(currentAngle * -0.006),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
                    SpeedRatio = AnimationSpeedRatio
                };
                exitRotateAnimation.Freeze();
                _indicatorRotation.BeginAnimation(RotateTransform.AngleProperty, exitRotateAnimation, HandoffBehavior.SnapshotAndReplace);
            }
        }

        private void StopAnimation()
        {
            _indeterminateAnimationRunning = false;
            _transitionAnimationRunning = false;
            _indicatorRotation.BeginAnimation(RotateTransform.AngleProperty, null, HandoffBehavior.SnapshotAndReplace);
            BeginAnimation(IndeterminateAnimatorProperty, null, HandoffBehavior.SnapshotAndReplace);
        }

        private void UpdateIndicator()
        {
            var offset = ActualWidth / 2;
            var radius = _arc0.Size.Width;

            double startPointRatio;
            double endPointRatio;
            if (IsIndeterminate || RunningTransitionAnimation)
            {
                startPointRatio = IndeterminateAnimator.X;
                endPointRatio = IndeterminateAnimator.Y;

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

            var startRadians = startPointRatio * Math.PI * 2;
            var endRadians = endPointRatio * Math.PI * 2;

            var mid0Radians = (((endPointRatio - startPointRatio) * 1 / 3) + startPointRatio) * Math.PI * 2;
            var mid1Radians = (((endPointRatio - startPointRatio) * 2 / 3) + startPointRatio) * Math.PI * 2;

            var startX = radius * Math.Sin(startRadians) + offset;
            var startY = -radius * Math.Cos(startRadians) + offset;

            var mid0X = radius * Math.Sin(mid0Radians) + offset;
            var mid0Y = -radius * Math.Cos(mid0Radians) + offset;

            var mid1X = radius * Math.Sin(mid1Radians) + offset;
            var mid1Y = -radius * Math.Cos(mid1Radians) + offset;

            var endX = radius * Math.Sin(endRadians) + offset;
            var endY = -radius * Math.Cos(endRadians) + offset;

            _arc2.Point = new Point(endX, endY);
            _arc1.Point = new Point(mid1X, mid1Y);
            _arc0.Point = new Point(mid0X, mid0Y);
            _arcsFigure.StartPoint = new Point(startX, startY);
        }

        private void RotateAnimation_Completed(object sender, EventArgs e)
        {
            if (_indeterminateAnimationRunning)
                _indicatorRotation.BeginAnimation(RotateTransform.AngleProperty, _rotateAnimation, HandoffBehavior.SnapshotAndReplace);
        }

        private void SpinAnimation_Completed(object sender, EventArgs e)
        {
            if (_indeterminateAnimationRunning)
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
                        Figures = new PathFigureCollection
                        {
                            new PathFigure
                            {
                                StartPoint = new Point(0, 0.1),
                                Segments = new PathSegmentCollection
                                {
                                    new PolyQuadraticBezierSegment
                                    {
                                        Points = new PointCollection
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
            var path = new FrameworkElementFactory(typeof(Path), IndicatorTemplateName);
            path.SetValue(Shape.StrokeThicknessProperty, new TemplateBindingExtension(CircleThicknessProperty));
            path.SetValue(Shape.StrokeProperty, new TemplateBindingExtension(ForegroundProperty));
            path.SetValue(Shape.StrokeStartLineCapProperty, PenLineCap.Round);
            path.SetValue(Shape.StrokeEndLineCapProperty, PenLineCap.Round);

            var ellipse = new FrameworkElementFactory(typeof(Ellipse), "ellipse");
            ellipse.SetValue(Shape.StrokeThicknessProperty, new TemplateBindingExtension(CircleThicknessProperty));
            ellipse.SetValue(Shape.StrokeProperty, new TemplateBindingExtension(BackgroundProperty));
            ellipse.SetValue(WidthProperty, new TemplateBindingExtension(ActualWidthProperty));
            ellipse.SetValue(HeightProperty, new TemplateBindingExtension(ActualWidthProperty));

            var canvas = new FrameworkElementFactory(typeof(Canvas), TrackTemplateName);
            canvas.AppendChild(ellipse);
            canvas.AppendChild(path);

            var root = new FrameworkElementFactory(typeof(Grid), "TemplateRoot");
            root.AppendChild(canvas);

            var ct = new ControlTemplate(typeof(RangeBase)) { 
                VisualTree = root
            };

            var style = new Style(typeof(RangeBase));
            style.Setters.Add(new Setter(TemplateProperty, ct));
            return style;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            /* *** PART_Track currentry not needed
            
            var track = GetTemplateChild(TrackTemplateName);
            if (track != null)
                _track = (FrameworkElement)track;
                _track.InvalidateProperty(RenderTransformProperty);
                //_track.RenderTransform = _trackTransform;
            */

            var indicator = GetTemplateChild(IndicatorTemplateName);
            if (indicator != null)
            {
                _indicator = (FrameworkElement)indicator;
                _indicator.InvalidateProperty(Path.DataProperty);
                _indicator.SetValue(Path.DataProperty, _indicatorGeometry);

                // *** Use geometry transform instead of RenderTransforms.
                //_indicator.InvalidateProperty(RenderTransformProperty);
                //_indicator.RenderTransform = _indicatorTransform;
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

        public double StrokeThickness
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

        private Point IndeterminateAnimator
        {
            get { return (Point)GetValue(IndeterminateAnimatorProperty); }
            set { SetValue(IndeterminateAnimatorProperty, value); }
        }

        private static readonly DependencyProperty IndeterminateAnimatorProperty =
            DependencyProperty.Register("IndeterminateAnimator", typeof(Point), typeof(ProgressSpinner),
                new FrameworkPropertyMetadata(new Point(), (d, e) =>
                {
                    ((ProgressSpinner)d).UpdateIndicator();
                }));

        #endregion Dependency properties

        #region Converters

        private class FactorConverter : IValueConverter
        {
            public static FactorConverter I = new FactorConverter();
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is double && parameter is double)
                {
                    return (double)value * (double)parameter;
                } else {
                    throw new ArgumentException();  
                }
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        private class ArcRadiusConverter : IMultiValueConverter
        {
            public static ArcRadiusConverter I = new ArcRadiusConverter();
            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                var actualWidth = (double)values[0];
                var strokeThickness = (double)values[1];

                if (actualWidth > strokeThickness)
                {
                    var radius = (actualWidth - strokeThickness) / 2;
                    return new Size(radius, radius);
                }

                return new Size();
            }

            public object[] ConvertBack(object value, Type[] targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        #endregion Converters
    }
}
