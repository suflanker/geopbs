using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel.Description;
using System.ServiceModel;

namespace PBS.Service
{
    /// WCF didn't provide ServiceThrottlingAttribute by default, so this is a custom Attribute.    /// 
    /// Programming WCF Services翻译笔记（八）
    /// http://www.cnblogs.com/wayfarer/archive/2007/11/12/956561.html
    public class ServiceThrottlingAttribute : Attribute, IServiceBehavior
    {
        private ServiceThrottlingBehavior throttle;

        public ServiceThrottlingAttribute(
          int maxConcurrentCalls,
          int maxConcurrentInstances,
          int maxConcurrentSessions)
        {
            this.throttle = new ServiceThrottlingBehavior();
            throttle.MaxConcurrentCalls = maxConcurrentCalls;
            throttle.MaxConcurrentInstances = maxConcurrentInstances;
            throttle.MaxConcurrentSessions = maxConcurrentSessions;
        }

        #region IServiceBehavior Members

        void IServiceBehavior.AddBindingParameters(ServiceDescription serviceDescription,
ServiceHostBase serviceHostBase,
System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints,
System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
        { }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription serviceDescription,
ServiceHostBase serviceHostBase)
        {
            ServiceThrottlingBehavior currentThrottle = serviceDescription.Behaviors.Find<ServiceThrottlingBehavior>();
            if (currentThrottle == null)
            {
                serviceDescription.Behaviors.Add(this.throttle);
            }
        }

        void IServiceBehavior.Validate(ServiceDescription serviceDescription,
 ServiceHostBase serviceHostBase)
        { }

        #endregion
    }
}
