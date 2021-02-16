using System; 
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core;

namespace ExtronXtpEpi
{
	public class IntWithFeedback
	{
		private int _Value;
		public int Value
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
		public IntFeedback Feedback;
		public IntWithFeedback()
		{
			Feedback = new IntFeedback(() => Value);
		}
	}
}