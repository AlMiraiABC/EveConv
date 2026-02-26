using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Plugins.Api
{
    /// <summary>
    /// Event callbacks to handle lifecycle.
    /// </summary>
    public interface ILifeCycle : IPlugin
    {
        /// <summary>
        /// Callback before install.
        /// </summary>
        void OnInstall()
        {

        }

        /// <summary>
        /// Callback when installed successfully.
        /// </summary>
        void AfterInstall()
        {

        }

        /// <summary>
        /// Callback before uninstall.
        /// </summary>
        void OnUninstall()
        {

        }

        ///// <summary>
        ///// Callback when uninstalled successfully.
        ///// </summary>
        //void AfterUninstall()
        //{

        //}

        /// <summary>
        /// Callback before update.
        /// </summary>
        void OnUpdate()
        {

        }

        /// <summary>
        /// Callback when updated successfully.
        /// </summary>
        void AfterUpdate()
        {

        }

        /// <summary>
        /// Callback before activate.
        /// </summary>
        void OnActivate()
        {

        }

        /// <summary>
        /// Callback when activated successfully.
        /// </summary>
        void AfterActivate()
        {

        }
    }
}
