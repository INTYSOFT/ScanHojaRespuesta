using ContrlAcademico.Services;
using System.Net.Http;

namespace ContrlAcademico
{
    public partial class LoginForm : Form
    {
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private readonly AuthService _authService = new();
        private ConfigModel? _config;

        public string AuthToken { get; private set; } = string.Empty;

        public LoginForm()
        {
            InitializeComponent();
            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                _config = ConfigModel.Load(_configPath);
                lblStatus.Text = string.Empty;
            }
            catch (Exception ex)
            {
                AuthControlsEnabled(false);
                lblStatus.Text = $"Error al cargar la configuración: {ex.Message}";
            }
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            if (_config is null)
            {
                lblStatus.Text = "No se pudo cargar la configuración del sistema.";
                return;
            }

            var username = txtUsername.Text.Trim();
            var password = txtPassword.Text;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                lblStatus.Text = "Ingrese usuario y contraseña.";
                return;
            }

            try
            {
                AuthControlsEnabled(false);
                lblStatus.Text = "Validando credenciales...";
                UseWaitCursor = true;

                var response = await _authService.LoginAsync(_config.ApiEndpoint, username, password);

                if (response is null)
                {
                    lblStatus.Text = "Usuario o contraseña incorrectos.";
                    return;
                }

                AuthToken = response.Token;

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (HttpRequestException ex)
            {
                lblStatus.Text = $"No se pudo conectar con el servidor: {ex.Message}";
            }
            catch (InvalidOperationException ex)
            {
                lblStatus.Text = ex.Message;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error al iniciar sesión: {ex.Message}";
            }
            finally
            {
                UseWaitCursor = false;
                AuthControlsEnabled(true);
            }
        }

        private void AuthControlsEnabled(bool enabled)
        {
            txtUsername.Enabled = enabled;
            txtPassword.Enabled = enabled;
            btnLogin.Enabled = enabled;
        }
    }
}

