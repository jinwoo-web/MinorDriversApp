using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MinorDriversApp.Models;

internal class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string pin1 = "";
    private string birth = "";
    private string expirationYear = "";
    private string secretCode = "";

    public string Pin1
    {
        get => pin1;
        set
        {
            pin1 = value;
            OnPropertyChanged();
        }
    }

    public string Birth
    {
        get => birth;
        set
        {
            birth = value;
            OnPropertyChanged();
        }
    }

    public string ExpirationYear
    {
        get => expirationYear;
        set
        {
            expirationYear = value;
            OnPropertyChanged();
        }
    }

    public string SecretCode
    {
        get => secretCode;
        set
        {
            secretCode = value;
            OnPropertyChanged();
        }
    }

    public string Pin2 => $"{birth}{expirationYear}{secretCode}";

    public void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
