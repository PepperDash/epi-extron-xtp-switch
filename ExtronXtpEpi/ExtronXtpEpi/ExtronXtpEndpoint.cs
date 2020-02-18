using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

using PepperDash.Essentials.Core;

namespace ExtronXtpEpi
{
    public abstract class ExtronXtpEndpoint
    {

    }

    public class ExtronXtpInput : ExtronXtpEndpoint, IRoutingOutputs
    {

        #region IRoutingOutputs Members

        public RoutingPortCollection<RoutingOutputPort> OutputPorts
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region IKeyed Members

        public string Key
        {
            get { throw new NotImplementedException(); }
        }

        #endregion
    }
}