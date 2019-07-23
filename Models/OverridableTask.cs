using System;
using System.Threading.Tasks;

namespace NanoLogViewer.Models
{
	class OverridableTask<T>
	{
		Task<T> recentTask;

		public void run(Func<T> calc, Action<T> then)
		{
			var task = recentTask = Task.Run(calc);

			Task.Run(() =>
			{
				var result = task.Result;
				lock (this)
				{
					if (task == recentTask)
					{
						then(result);
					}
				}
			});
		}
	}
}
