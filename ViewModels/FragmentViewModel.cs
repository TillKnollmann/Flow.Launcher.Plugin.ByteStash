using System.IO;
using System.Reflection;
using System.Windows.Input;
using ByteStashClient;
using Flow.Launcher.Plugin.ByteStash.Helpers;
using Flow.Launcher.Plugin.ByteStash.Resources;

namespace Flow.Launcher.Plugin.ByteStash.ViewModels
{
    /// <summary>
    /// ViewModel for a code fragment in the snippet preview.
    /// </summary>
    public class FragmentViewModel
    {
        private readonly IPublicAPI _api;

        /// <summary>
        /// Initializes a new instance of the <see cref="FragmentViewModel"/> class.
        /// </summary>
        /// <param name="fragment">The fragment data.</param>
        /// <param name="api">The Flow Launcher API.</param>
        public FragmentViewModel(Fragment fragment, IPublicAPI api)
        {
            Fragment = fragment;
            _api = api;
            CopyCommand = new RelayCommand(CopyCode);

            // Set the copy icon path
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            CopyIconPath = Path.Combine(assemblyPath, "Icons", Icon.COPY);
        }

        /// <summary>
        /// Gets the fragment data.
        /// </summary>
        public Fragment Fragment { get; }

        /// <summary>
        /// Gets the command to copy the code.
        /// </summary>
        public ICommand CopyCommand { get; }

        /// <summary>
        /// Gets the path to the copy icon.
        /// </summary>
        public string CopyIconPath { get; }

        /// <summary>
        /// Copies the fragment code to clipboard.
        /// </summary>
        private void CopyCode()
        {
            if (!string.IsNullOrEmpty(Fragment.Code))
            {
                _api.CopyToClipboard(Fragment.Code);
                _api.ShowMsg(Strings.Preview_CodeCopied_Title, Strings.Preview_CodeCopied_Message);
            }
        }
    }

    /// <summary>
    /// Simple relay command implementation.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="RelayCommand"/> class.
    /// </remarks>
    /// <param name="execute">The execute action.</param>
    /// <param name="canExecute">The can execute function.</param>
    public class RelayCommand(System.Action execute, System.Func<bool> canExecute = null) : ICommand
    {
        private readonly System.Action _execute = execute ?? throw new System.ArgumentNullException(nameof(execute));
        private readonly System.Func<bool> _canExecute = canExecute;

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// </summary>
        public event System.EventHandler CanExecuteChanged;

        /// <summary>
        /// Raises the CanExecuteChanged event.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
        }

        /// <summary>
        /// Determines whether the command can execute.
        /// </summary>
        /// <param name="parameter">The command parameter.</param>
        /// <returns>True if the command can execute; otherwise, false.</returns>
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="parameter">The command parameter.</param>
        public void Execute(object parameter)
        {
            _execute();
        }
    }
}
