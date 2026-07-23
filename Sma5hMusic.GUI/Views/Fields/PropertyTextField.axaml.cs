using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Windows.Input;

namespace Sma5hMusic.GUI.Views.Fields
{
    public class PropertyTextField : UserControl
    {
        public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<PropertyTextField, string>(nameof(Label), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<string> ToolTipProperty = AvaloniaProperty.Register<PropertyTextField, string>(nameof(ToolTip), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<PropertyTextField, string>(nameof(Text), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<PropertyTextField, bool>(nameof(IsReadOnly), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<bool> IsRequiredProperty = AvaloniaProperty.Register<PropertyField, bool>(nameof(IsRequired), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<bool> AcceptsReturnProperty = AvaloniaProperty.Register<PropertyTextField, bool>(nameof(AcceptsReturn), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<string> ValidationErrorProperty = AvaloniaProperty.Register<PropertyTextField, string>(nameof(ValidationError), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<string> ButtonContentProperty = AvaloniaProperty.Register<PropertyTextField, string>(nameof(ButtonContent), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<ICommand> ButtonCommandProperty = AvaloniaProperty.Register<PropertyTextField, ICommand>(nameof(ButtonCommand), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<object> ButtonCommandParameterProperty = AvaloniaProperty.Register<PropertyTextField, object>(nameof(ButtonCommandParameter), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);
        public static readonly StyledProperty<bool> IsButtonVisibleProperty = AvaloniaProperty.Register<PropertyTextField, bool>(nameof(IsButtonVisible), inherits: true, defaultBindingMode: Avalonia.Data.BindingMode.OneWay);

        public string ValidationError
        {
            get { return GetValue(ValidationErrorProperty); }
            set { SetValue(ValidationErrorProperty, value); }
        }

        public string ToolTip
        {
            get { return GetValue(ToolTipProperty); }
            set { SetValue(ToolTipProperty, value); }
        }

        public string Label
        {
            get { return GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public string Text
        {
            get { return GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public bool IsReadOnly
        {
            get { return GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        public bool IsRequired
        {
            get { return GetValue(IsRequiredProperty); }
            set { SetValue(IsRequiredProperty, value); }
        }

        public bool AcceptsReturn
        {
            get { return GetValue(AcceptsReturnProperty); }
            set { SetValue(AcceptsReturnProperty, value); }
        }

        public string ButtonContent
        {
            get { return GetValue(ButtonContentProperty); }
            set { SetValue(ButtonContentProperty, value); }
        }

        public ICommand ButtonCommand
        {
            get { return GetValue(ButtonCommandProperty); }
            set { SetValue(ButtonCommandProperty, value); }
        }

        public object ButtonCommandParameter
        {
            get { return GetValue(ButtonCommandParameterProperty); }
            set { SetValue(ButtonCommandParameterProperty, value); }
        }

        public bool IsButtonVisible
        {
            get { return GetValue(IsButtonVisibleProperty); }
            set { SetValue(IsButtonVisibleProperty, value); }
        }

        public PropertyTextField()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
