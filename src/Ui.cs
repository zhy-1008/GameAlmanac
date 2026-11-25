using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace GameAlmanac
{
    internal static class Ui
    {
        public static Brush Ink { get { return Brush("Ink"); } }
        public static Brush Muted { get { return Brush("Muted"); } }
        public static Brush Base { get { return Brush("Base"); } }
        public static Brush Paper { get { return Brush("Paper"); } }
        public static Brush Line { get { return Brush("Line"); } }
        public static Brush Green { get { return Brush("Green"); } }
        public static Brush Pale { get { return Brush("Pale"); } }

        public static Brush Brush(string key) { return (Brush)Application.Current.FindResource(key); }
        public static SolidColorBrush Color(string value)
        {
            var color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
            color.Freeze();
            return color;
        }

        public static TextBlock Text(string value, double size = 13, Brush color = null, bool bold = false)
        {
            return new TextBlock {
                Text = value, FontSize = size, Foreground = color ?? Ink,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
            };
        }

        public static TextBlock Number(string value, double size, Brush color = null, bool bold = false)
        {
            var text = Text(value, size, color, bold);
            text.FontFamily = new FontFamily("Bahnschrift, Segoe UI");
            return text;
        }

        public static Button Button(string caption, Action action, string style = null)
        {
            var button = new Button { Content = caption };
            if (style != null) button.SetResourceReference(FrameworkElement.StyleProperty, style);
            button.Click += (sender, args) => action();
            return button;
        }

        public static Border Card(UIElement content, Brush background = null, double padding = 15)
        {
            return new Border {
                Child = content, Padding = new Thickness(padding), Background = background ?? Paper,
                BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7)
            };
        }

        public static ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer {
                Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly, Focusable = false
            };
        }

        public static DockPanel Spread(UIElement left, UIElement right)
        {
            var row = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(right, Dock.Right);
            row.Children.Add(right);
            row.Children.Add(left);
            return row;
        }

        public static StackPanel LineUp(params UIElement[] controls)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var control in controls) panel.Children.Add(control);
            return panel;
        }

        public static void Put(Grid grid, UIElement element, int column, int row = 0)
        {
            Grid.SetColumn(element, column);
            Grid.SetRow(element, row);
            grid.Children.Add(element);
        }

        public static ColumnDefinition Pixels(double width) { return new ColumnDefinition { Width = new GridLength(width) }; }
        public static ColumnDefinition Star(double proportion = 1)
        {
            return new ColumnDefinition { Width = new GridLength(proportion, GridUnitType.Star) };
        }

        public static TextBox Box(string value, string label, int maxLength = 100)
        {
            var box = new TextBox { Text = value ?? "", MaxLength = maxLength };
            AutomationProperties.SetName(box, label);
            return box;
        }

        public static StackPanel Field(string caption, Control input)
        {
            var field = new StackPanel { Margin = new Thickness(0, 0, 0, 11) };
            var label = new Label { Content = caption, Target = input, FontWeight = FontWeights.SemiBold,
                Foreground = Ink, Padding = new Thickness(0, 0, 0, 4) };
            AutomationProperties.SetLabeledBy(input, label);
            field.Children.Add(label);
            field.Children.Add(input);
            return field;
        }

        public static FrameworkElement Bar(double value, double max)
        {
            var track = new Grid { Height = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 10, 0) };
            track.Children.Add(new Border { Background = Color("#E1EADF"), CornerRadius = new CornerRadius(4) });
            double fraction = Math.Max(0, Math.Min(1, value / max));
            track.ColumnDefinitions.Add(Star(fraction));
            track.ColumnDefinitions.Add(Star(1 - fraction));
            Grid.SetColumnSpan(track.Children[0], 2);
            track.Children.Add(new Border { Background = Color("#548872"), CornerRadius = new CornerRadius(4) });
            return track;
        }

        public static FrameworkElement Cover(BitmapSource image, string name)
        {
            if (image != null)
            {
                var art = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
                return new Border { Background = art, CornerRadius = new CornerRadius(3) };
            }
            var text = Text(name, 16, Color("#EDE7CF"), true);
            text.TextAlignment = TextAlignment.Center;
            return new Border {
                Background = Color("#435F51"), CornerRadius = new CornerRadius(3), Padding = new Thickness(8), Child = text
            };
        }

        public static string Percent(double number)
        {
            return number.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        public static void SetWindow(Window window, string title)
        {
            window.Title = title;
            window.FontFamily = new FontFamily("Microsoft YaHei UI");
            window.FontSize = 13;
            window.Foreground = Ink;
            window.Background = Base;
            window.UseLayoutRounding = true;
        }
    }

    internal sealed class WindowFrame : DockPanel
    {
        private readonly Border body = new Border();

        public WindowFrame(Window window, bool fullControls, string caption = null)
        {
            window.WindowStyle = WindowStyle.None;
            WindowChrome.SetWindowChrome(window, new WindowChrome
            {
                CaptionHeight = fullControls ? 38 : 44,
                ResizeBorderThickness = new Thickness(fullControls ? 6 : 0),
                GlassFrameThickness = new Thickness(0),
                UseAeroCaptionButtons = false,
                CornerRadius = new CornerRadius(0)
            });

            var heading = Ui.Text(caption ?? window.Title, 14, Ui.Ink, true);
            if (caption == null) heading.SetBinding(TextBlock.TextProperty, new Binding("Title") { Source = window });
            heading.TextWrapping = TextWrapping.NoWrap;
            heading.TextTrimming = TextTrimming.CharacterEllipsis;
            heading.Margin = new Thickness(8, 0, 5, 0);
            var logo = new Border
            {
                Width = 23, Height = 23, Background = Ui.Green, CornerRadius = new CornerRadius(5),
                Child = Icon("M2,2 H16 V15 H2 Z M2,8 H16 M6,2 V8 M10,8 V15 M13,2 V8", Brushes.White, 13)
            };
            var brand = new DockPanel { Margin = new Thickness(15, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            SetDock(logo, Dock.Left);
            brand.Children.Add(logo);
            brand.Children.Add(heading);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            if (fullControls)
            {
                buttons.Children.Add(CaptionButton("最小化", "M2,7 H12", () => SystemCommands.MinimizeWindow(window)));
                var resize = CaptionButton("最大化", "M2,2 H12 V12 H2 Z", () =>
                {
                    if (window.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(window);
                    else SystemCommands.MaximizeWindow(window);
                });
                window.StateChanged += (sender, args) =>
                {
                    bool maximized = window.WindowState == WindowState.Maximized;
                    resize.ToolTip = maximized ? "还原" : "最大化";
                    AutomationProperties.SetName(resize, (string)resize.ToolTip);
                    ((Path)resize.Content).Data = Geometry.Parse(maximized ? "M4,2 H12 V10 M2,4 H10 V12 H2 Z" : "M2,2 H12 V12 H2 Z");
                };
                buttons.Children.Add(resize);
            }
            buttons.Children.Add(CaptionButton("关闭", "M2,2 L12,12 M12,2 L2,12", window.Close, "CaptionClose"));

            var titleBar = new Border
            {
                Height = 44, Background = Ui.Brush("Chrome"), BorderBrush = Ui.Line,
                BorderThickness = new Thickness(0, 0, 0, 1), Child = Ui.Spread(brand, buttons)
            };
            SetDock(titleBar, Dock.Top);
            Children.Add(titleBar);
            Children.Add(body);
        }

        public void SetBody(UIElement content) { body.Child = content; }

        private static Button CaptionButton(string name, string geometry, Action action, string style = "CaptionButton")
        {
            var button = Ui.Button(name, action, style);
            var icon = Icon(geometry, Ui.Ink, 11);
            icon.SetBinding(Shape.StrokeProperty, new Binding("Foreground") { Source = button });
            button.Content = icon;
            button.ToolTip = name;
            AutomationProperties.SetName(button, name);
            WindowChrome.SetIsHitTestVisibleInChrome(button, true);
            return button;
        }

        private static Path Icon(string geometry, Brush color, double size)
        {
            return new Path
            {
                Data = Geometry.Parse(geometry), Stroke = color, StrokeThickness = 1.15,
                Stretch = Stretch.Uniform, Width = size, Height = size,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
        }
    }

    internal sealed class FormWindow : Window
    {
        public readonly StackPanel Body = new StackPanel();
        public readonly Button Save;
        public readonly Button Cancel;
        private readonly TextBlock error = Ui.Text("", 12, Ui.Brush("Error"));

        public FormWindow(Window owner, string title, string saveLabel, Func<bool> commit, double width = 520, bool destructive = false)
        {
            Ui.SetWindow(this, title);
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Height;
            Width = width;
            MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 64);
            Background = Ui.Paper;

            var shell = new DockPanel();
            Save = Ui.Button(saveLabel, () =>
            {
                error.Text = "";
                try { if (commit()) DialogResult = true; }
                catch (Exception exception) { error.Text = exception.Message; }
            }, destructive ? "DangerButton" : "PrimaryButton");
            Save.IsDefault = !destructive;
            Cancel = Ui.Button("取消", () => { DialogResult = false; });
            Cancel.IsCancel = true;
            Cancel.IsDefault = destructive;
            Save.Margin = new Thickness(8, 0, 0, 0);
            var actions = Ui.LineUp(Cancel, Save);
            error.Margin = new Thickness(0, 0, 8, 0);
            var footer = new Border {
                Background = Ui.Paper, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(23, 10, 23, 10), Child = Ui.Spread(error, actions)
            };
            DockPanel.SetDock(footer, Dock.Bottom);
            shell.Children.Add(footer);

            Body.Margin = new Thickness(23, 16, 23, 9);
            shell.Children.Add(Ui.Scroll(Body));
            var frame = new WindowFrame(this, false);
            frame.SetBody(shell);
            Content = frame;
        }

        public void Fail(string message) { error.Text = message; }
    }
}
