using System;
using System.Threading;

namespace NanoLogViewer.Models
{
	class BackgroundThread
	{
		Thread thread;
		ManualResetEvent resetEvent;

		public bool isRunning => thread != null;

		public void run(Action<ManualResetEvent> work, Action after=null)
		{
			if (isRunning) throw new Exception("Thread already running.");

			resetEvent = new ManualResetEvent(false);

			thread = new Thread(() =>
			{
				try
				{
					work(resetEvent);
				}
				finally
				{
					thread = null;
					after?.Invoke();
				}
			});

			thread.Start();
		}

		public void wantToStop()
		{
			resetEvent.Set();
		}
	}
}
