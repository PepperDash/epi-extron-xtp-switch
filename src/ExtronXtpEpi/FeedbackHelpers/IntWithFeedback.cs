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