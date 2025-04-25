using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace PokerGame.Avalonia.ViewModels
{
    /// <summary>
    /// View model for the main window
    /// </summary>
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase _contentViewModel;
        
        /// <summary>
        /// Creates a new instance of the MainWindowViewModel
        /// </summary>
        public MainWindowViewModel()
        {
            // Start with the game view model
            _contentViewModel = new GameViewModel();
        }
        
        /// <summary>
        /// Gets or sets the current content view model
        /// </summary>
        public ViewModelBase ContentViewModel
        {
            get => _contentViewModel;
            set 
            {
                this.RaiseAndSetIfChanged(ref _contentViewModel, value);
            }
        }
    }
}
