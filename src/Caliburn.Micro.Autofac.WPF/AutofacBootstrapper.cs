﻿using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;

namespace Caliburn.Micro.Autofac
{
    /// <summary>
    /// A strongly-typed version of Caliburn.Micro.Bootstrapper that specifies the type of root model to create for the application.
    /// </summary>
    /// <typeparam name="TRootViewModel">The type of root view model for the application.</typeparam>
    public class AutofacBootstrapper<TRootViewModel> : Caliburn.Micro.BootstrapperBase
    {
        #region Properties

        protected IContainer Container { get; private set; }

        /// <summary>
        /// Should the namespace convention be enforced for type registration. The default is true.
        /// For views, this would require a views namespace to end with Views
        /// For view-models, this would require a view models namespace to end with ViewModels
        /// <remarks>Case is important as views would not match.</remarks>
        /// </summary>
        public bool EnforceNamespaceConvention { get; set; }

        /// <summary>
        /// Should the IoC automatically subscribe any types found that implement the
        /// IHandle interface at activation
        /// </summary>
        public bool AutoSubscribeEventAggegatorHandlers { get; set; }

        /// <summary>
        /// The base type required for a view model
        /// </summary>
        public Type ViewModelBaseType { get; set; }

        /// <summary>
        /// Method for creating the window manager
        /// </summary>
        public Func<IWindowManager> CreateWindowManager { get; set; }

        /// <summary>
        /// Method for creating the event aggregator
        /// </summary>
        public Func<IEventAggregator> CreateEventAggregator { get; set; }

        #endregion

        /// <summary>
        /// Checks whether to register the specified <c>View</c> type.
        /// </summary>
        /// <remarks>Override this method in order to apply additional registration rules.</remarks>
        /// <param name="type">Type of the <c>View</c> to check.</param>
        /// <returns><b>true</b> if <c>View</c> must be registered; <b>false</b> otherwise.</returns>
        protected virtual bool CheckRegisterView(Type type)
        {
            //  must be a type with a name that ends with View
            if (!type.Name.EndsWith("View"))
                return false;

            //  must be in a namespace that ends in Views
            if (EnforceNamespaceConvention)
                return !string.IsNullOrWhiteSpace(type.Namespace) && type.Namespace.EndsWith("Views");

            return true;
        }

        /// <summary>
        /// Checks whether to register the specified <c>ViewModel</c> type.
        /// </summary>
        /// <remarks>Override this method in order to apply additional registration rules.</remarks>
        /// <param name="type">Type of the <c>ViewModel</c> to check.</param>
        /// <returns><b>true</b> if <c>ViewModel</c> must be registered; <b>false</b> otherwise.</returns>
        protected virtual bool CheckRegisterViewModel(Type type)
        {
            //  must be a type with a name that ends with ViewModel
            if (!type.Name.EndsWith("ViewModel"))
                return false;

            //  must be in a namespace ending with ViewModels
            if (EnforceNamespaceConvention)
                return !string.IsNullOrWhiteSpace(type.Namespace) && type.Namespace.EndsWith("ViewModels");

            //  must implement INotifyPropertyChanged (deriving from PropertyChangedBase will satisfy this)
            return type.GetInterface(ViewModelBaseType.Name, false) != null;
        }

        /// <summary>
        /// Do not override this method. This is where the IoC container is configured.
        /// <remarks>
        /// Will throw <see cref="System.ArgumentNullException"/> is either CreateWindowManager
        /// or CreateEventAggregator is null.
        /// </remarks>
        /// </summary>
        protected override void Configure()
        { //  allow base classes to change bootstrapper settings
            ConfigureBootstrapper();

            //  validate settings
            if (CreateWindowManager == null)
                throw new ArgumentNullException(nameof(CreateWindowManager));

            if (CreateEventAggregator == null)
                throw new ArgumentNullException(nameof(CreateEventAggregator));

            //  configure container
            var builder = new ContainerBuilder();

            //  register view models
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                .Where(CheckRegisterViewModel)
                .AsSelf() 
                //  always create a new one
                .InstancePerDependency();

            //  register views
            builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())
                .Where(CheckRegisterView) 
                //  registered as self
                .AsSelf() 
                //  always create a new one
                .InstancePerDependency();

            //  register the single window manager for this container
            builder.Register<IWindowManager>(c => CreateWindowManager()).InstancePerLifetimeScope();
            //  register the single event aggregator for this container
            builder.Register<IEventAggregator>(c => CreateEventAggregator()).InstancePerLifetimeScope();

            //  should we install the auto-subscribe event aggregation handler module?
            if (AutoSubscribeEventAggegatorHandlers)
                builder.RegisterModule<EventAggregationAutoSubscriptionModule>();

            //  allow derived classes to add to the container
            ConfigureContainer(builder);

            Container = builder.Build();
        }

        /// <summary>
        /// Do not override unless you plan to full replace the logic. This is how the framework
        /// retrieves services from the Autofac container.
        /// </summary>
        /// <param name="service">The service to locate.</param>
        /// <param name="key">The key to locate.</param>
        /// <returns>The located service.</returns>
        protected override object GetInstance(System.Type service, string key)
        {
            object instance;
            if (string.IsNullOrWhiteSpace(key))
            {
                if (Container.TryResolve(service, out instance))
                    return instance;
            }
            else
            {
                if (Container.TryResolveNamed(key, service, out instance))
                    return instance;
            }
            throw new Exception(string.Format("Could not locate any instances of contract {0}.", key ?? service.Name));
        }

        /// <summary>
        /// Do not override unless you plan to full replace the logic. This is how the framework
        /// retrieves services from the Autofac container.
        /// </summary>
        /// <param name="service">The service to locate.</param>
        /// <returns>The located services.</returns>
        protected override System.Collections.Generic.IEnumerable<object> GetAllInstances(System.Type service)
        {
            return Container.Resolve(typeof(IEnumerable<>).MakeGenericType(service)) as IEnumerable<object>;
        }

        /// <summary>
        /// Do not override unless you plan to full replace the logic. This is how the framework
        /// retrieves services from the Autofac container.
        /// </summary>
        /// <param name="instance">The instance to perform injection on.</param>
        protected override void BuildUp(object instance)
        {
            Container.InjectProperties(instance);
        }

        /// <summary>
        /// Override to provide configuration prior to the Autofac configuration. You must call the base version BEFORE any
        /// other statement or the behaviour is undefined.
        /// Current Defaults:
        ///   EnforceNamespaceConvention = true
        ///   ViewModelBaseType = <see cref="System.ComponentModel.INotifyPropertyChanged"/>
        ///   CreateWindowManager = <see cref="Caliburn.Micro.WindowManager"/>
        ///   CreateEventAggregator = <see cref="Caliburn.Micro.EventAggregator"/>
        /// </summary>
        protected virtual void ConfigureBootstrapper()
        { //  by default, enforce the namespace convention
            EnforceNamespaceConvention = true;
            // default is to auto subscribe known event aggregators
            AutoSubscribeEventAggegatorHandlers = false;
            //  the default view model base type
            ViewModelBaseType = typeof(System.ComponentModel.INotifyPropertyChanged);
            //  default window manager
            CreateWindowManager = () => new WindowManager();
            //  default event aggregator
            CreateEventAggregator = () => new EventAggregator();
        }

        /// <summary>
        /// Override to include your own Autofac configuration after the framework has finished its configuration, but
        /// before the container is created.
        /// </summary>
        /// <param name="builder">The Autofac configuration builder.</param>
        protected virtual void ConfigureContainer(ContainerBuilder builder)
        {
        }

        /// <summary>Override this to add custom behavior on exit.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        protected override void OnExit(object sender, EventArgs e)
        {
            this.Container.Dispose();
        }
    }
}
