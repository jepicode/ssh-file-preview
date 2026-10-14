using System.Windows;

namespace SshFilePreview;

public partial class SshTargetDialog : Window
{
    public SshTargetDialog(string currentTarget)
    {
        InitializeComponent();
        TargetBox.Text = currentTarget;
        TargetBox.SelectAll();
        TargetBox.Focus();
    }

    public string Target => TargetBox.Text.Trim();

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (!MainWindow.IsValidSshTarget(Target))
        {
            ValidationMessage.Text = "Gunakan alias SSH atau format user@host tanpa spasi atau opsi SSH.";
            TargetBox.Focus();
            TargetBox.SelectAll();
            return;
        }

        DialogResult = true;
    }
}
