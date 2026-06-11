using System.Windows.Controls;
using MahApps.Metro.IconPacks;
using System.Windows;

namespace CryptoSelfBot.Wpf.Controls
{
    public partial class IconButton : UserControl
    {
        public IconButton()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
            "Kind", typeof(PackIconMaterialKind), typeof(IconButton), new PropertyMetadata(PackIconMaterialKind.Refresh));

        public PackIconMaterialKind Kind
        {
            get => (PackIconMaterialKind)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }
    }
}
