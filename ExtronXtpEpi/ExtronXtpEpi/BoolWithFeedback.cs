using System; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core;

namespace ExtronXtpEpi
{
	public class BoolWithFeedback
	{
		private bool _Value;
		public bool Value
		{
			get
			{
				return _Value;
			}

			set 
			{
				_Value = value;
				Feedback.FireUpdate();
			}
		}
		public BoolFeedback Feedback;
		public BoolWithFeedback()
		{
			Feedback = new BoolFeedback(() => Value);
		}
	}
}