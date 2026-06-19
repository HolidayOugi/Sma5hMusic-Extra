using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Sma5hMusic.GUI.Controls;
using System;

namespace Sma5hMusic.GUI.Views
{
    public class MSBTFieldView : UserControl
    {
        private MsbtRichTextEditorController _richTextController;

        public MSBTFieldView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _richTextController = new MsbtRichTextEditorController(
                this.FindControl<TextBox>("RichTextEditor"),
                this.FindControl<StackPanel>("RichTextPreview"),
                this.FindControl<ComboBox>("TextColorComboBox"),
                this.FindControl<Button>("SmallTextMarkerButton"));
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            _richTextController?.SetDataContext(DataContext);
        }
    }
}
