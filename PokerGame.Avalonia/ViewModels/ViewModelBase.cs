using ReactiveUI;
using System;
using System.Collections.Generic;

namespace PokerGame.Avalonia.ViewModels
{
    /// <summary>
    /// Base class for all view models
    /// </summary>
    public class ViewModelBase : ReactiveObject
    {
        /// <summary>
        /// Raises the property changed event
        /// </summary>
        /// <param name="propertyName">The name of the property that changed</param>
        protected void RaisePropertyChanged(string propertyName)
        {
            this.RaisePropertyChanged(propertyName);
        }
    }
}
