using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace EpgTimer
{
    /// <summary>
    /// 数値入力用のテキストボックス。右側の▲▼(または↑↓キー)で値を増減できる。
    /// Textは通常のTextBoxと同じように文字列としてバインドして使う
    /// </summary>
    public class SpinTextBox : Grid
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text", typeof(string), typeof(SpinTextBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        /// <summary>▲▼1回で増減する量</summary>
        public double Step { get; set; }
        /// <summary>▼で下げられる最小値</summary>
        public double Minimum { get; set; }
        /// <summary>空欄や0など、数値として使えない状態のときに、増減の基準にする値</summary>
        public double Fallback { get; set; }

        private readonly TextBox textBox;

        public SpinTextBox()
        {
            Step = 1;
            Minimum = 1;
            Fallback = 12;
            Width = 120;
            HorizontalAlignment = HorizontalAlignment.Left;

            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            textBox = new TextBox();
            if (Application.Current != null)
            {
                Style style = Application.Current.TryFindResource("AppTextBoxStyle") as Style;
                if (style != null)
                {
                    textBox.Style = style;
                }
            }
            InputMethod.SetIsInputMethodEnabled(textBox, false);
            textBox.LostFocus += (sender, e) => Commit();
            textBox.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    StepBy(e.Key == Key.Up ? 1 : -1);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    Commit();
                }
            };
            Children.Add(textBox);

            var buttons = new Grid();
            buttons.RowDefinitions.Add(new RowDefinition());
            buttons.RowDefinitions.Add(new RowDefinition());
            Grid.SetColumn(buttons, 1);
            RepeatButton up = CreateButton("\u25B2", 1);
            RepeatButton down = CreateButton("\u25BC", -1);
            Grid.SetRow(down, 1);
            buttons.Children.Add(up);
            buttons.Children.Add(down);
            Children.Add(buttons);
        }

        private RepeatButton CreateButton(string content, int direction)
        {
            var button = new RepeatButton
            {
                Content = content,
                Focusable = false,
                FontSize = 7,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                MinWidth = 18,
                MinHeight = 0,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            button.Click += (sender, e) => StepBy(direction);
            return button;
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (SpinTextBox)d;
            string text = (string)e.NewValue ?? string.Empty;
            if (control.textBox.Text != text)
            {
                control.textBox.Text = text;
            }
        }

        /// <summary>入力中の文字列を、バインド元に反映する</summary>
        private void Commit()
        {
            if (Text != textBox.Text)
            {
                SetCurrentValue(TextProperty, textBox.Text);
            }
        }

        private void StepBy(int direction)
        {
            double value;
            if (double.TryParse(textBox.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) == false || value <= 0)
            {
                value = Fallback;
            }
            value += direction * Step;
            if (value < Minimum)
            {
                value = Minimum;
            }
            textBox.Text = value.ToString(CultureInfo.InvariantCulture);
            Commit();
        }
    }
}
