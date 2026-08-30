using System;
using System.Text.Json.Serialization;

using CommunityToolkit.Mvvm.ComponentModel;

namespace DevBoard.Models
{
    public enum GitHubAuthType
    {
        PersonalAccessToken = 0,
        SSHKey = 1,
    }

    public partial class GitHubAccount : ObservableObject
    {
        public GitHubAccount()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public Guid Id { get; set; }

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private GitHubAuthType _authType = GitHubAuthType.PersonalAccessToken;

        private string _sshKeyPath = string.Empty;
        public string SSHKeyPath
        {
            get => _sshKeyPath;
            set
            {
                if (SetProperty(ref _sshKeyPath, value ?? string.Empty))
                    OnPropertyChanged(nameof(HasValidCredentials));
            }
        }

        [ObservableProperty]
        private bool _isDefault;

        [ObservableProperty]
        private string _avatarUrl = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [JsonIgnore]
        public string Token
        {
            get => string.IsNullOrEmpty(_token) ? Services.CredentialManager.GetToken(Id) : _token;
            set
            {
                _token = value ?? string.Empty;
                Services.CredentialManager.StoreToken(Id, _token);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidCredentials));
            }
        }

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Username : Name;

        [JsonIgnore]
        public bool HasValidCredentials => AuthType switch
        {
            GitHubAuthType.PersonalAccessToken => !string.IsNullOrWhiteSpace(Token),
            GitHubAuthType.SSHKey => !string.IsNullOrWhiteSpace(SSHKeyPath),
            _ => false,
        };

        public void DeleteCredentials()
        {
            _token = string.Empty;
            Services.CredentialManager.DeleteToken(Id);
            OnPropertyChanged(nameof(Token));
            OnPropertyChanged(nameof(HasValidCredentials));
        }

        private string _token = string.Empty;
    }
}
