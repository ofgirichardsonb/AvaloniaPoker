using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
        protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (propertyName != null)
            {
                ((IReactiveObject)this).RaisePropertyChanged(propertyName);
            }
        }
        
        /// <summary>
        /// Sets the property value and raises the property changed event if the value has changed
        /// </summary>
        /// <typeparam name="T">The type of the property</typeparam>
        /// <param name="field">The field to update</param>
        /// <param name="value">The new value</param>
        /// <param name="propertyName">The name of the property</param>
        /// <returns>True if the property changed, false otherwise</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            
            field = value;
            if (propertyName != null)
            {
                ((IReactiveObject)this).RaisePropertyChanged(propertyName);
            }
            
            return true;
        }
    }
}
