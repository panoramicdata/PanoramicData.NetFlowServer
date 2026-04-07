using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ExampleApp;

/// <summary>
/// Wraps an external process and exposes its I/O as events.
/// </summary>
public class CommandService
{
	private Process? _process;
	private readonly ProcessStartInfo _startInfo;

	/// <summary>
	/// Initializes a new instance of the <see cref="CommandService"/> class.
	/// </summary>
	/// <param name="command">The command to execute.</param>
	/// <param name="args">The arguments to pass to the command.</param>
	public CommandService(string command, string args)
	{
		_startInfo = new ProcessStartInfo(command, args)
		{
			CreateNoWindow = true,
			RedirectStandardError = true,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			UseShellExecute = false,
		};
	}

	/// <summary>
	/// Occurs when data is received from the process standard output.
	/// </summary>
	public event EventHandler<byte[]>? DataReceived;

	/// <summary>
	/// Occurs when the process standard output stream reaches end-of-file.
	/// </summary>
	public event EventHandler? EofReceived;

	/// <summary>
	/// Occurs when the process has closed.
	/// </summary>
	public event EventHandler<uint>? CloseReceived;

	/// <summary>
	/// Starts the process.
	/// </summary>
	public void Start()
	{
		_process = Process.Start(_startInfo) ?? throw new InvalidOperationException("Failed to start process.");
		Task.Run(() => MessageLoop());
	}

	/// <summary>
	/// Writes data to the process standard input.
	/// </summary>
	/// <param name="data">The data to write.</param>
	public void OnData(byte[] data)
	{
		_process!.StandardInput.BaseStream.Write(data, 0, data.Length);
		_process.StandardInput.BaseStream.Flush();
	}

	/// <summary>
	/// Closes the process standard input stream.
	/// </summary>
	public void OnClose() => _process!.StandardInput.BaseStream.Close();

	private void MessageLoop()
	{
		var bytes = new byte[1024 * 64];
		while (true)
		{
			var len = _process!.StandardOutput.BaseStream.Read(bytes, 0, bytes.Length);
			if (len <= 0)
				break;

			var data = bytes.Length != len
				? [.. bytes.Take(len)]
				: bytes;
			DataReceived?.Invoke(this, data);
		}

		EofReceived?.Invoke(this, EventArgs.Empty);
		CloseReceived?.Invoke(this, (uint)_process.ExitCode);
	}
}
