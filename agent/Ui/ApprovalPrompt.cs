using System.Windows.Forms;

namespace RebelDesk.Agent.Ui;

public sealed record ApprovalRequest(Guid SessionId, string InitiatorUserId, string? InitiatorIp, DateTime CreatedAtUtc);

/// <summary>
/// Blocks until the local user clicks Approve or Reject on a topmost dialog.
/// Every incoming session is gated by this prompt. Auto-rejects after <see cref="TimeoutSeconds"/>
/// if the local user does not respond, so a session request cannot silently hang.
/// </summary>
public sealed class ApprovalPrompt
{
    public int TimeoutSeconds { get; init; } = 30;

    public Task<bool> AskAsync(ApprovalRequest req, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                using var form = new Form
                {
                    Text = "RebelDesk — Session request",
                    StartPosition = FormStartPosition.CenterScreen,
                    ClientSize = new System.Drawing.Size(460, 220),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    TopMost = true,
                };

                var label = new Label
                {
                    Location = new System.Drawing.Point(20, 20),
                    Size = new System.Drawing.Size(420, 130),
                    Font = new System.Drawing.Font("Segoe UI", 10f),
                    Text =
                        $"A viewer is requesting remote control of this computer.\n\n" +
                        $"Session ID: {req.SessionId}\n" +
                        $"From: {req.InitiatorIp ?? "unknown"}\n" +
                        $"Requested at: {req.CreatedAtUtc:HH:mm:ss} UTC\n\n" +
                        $"Approve only if you started this session.\n" +
                        $"Auto-reject in {TimeoutSeconds}s.",
                };

                var approve = new Button
                {
                    Text = "&Approve",
                    Location = new System.Drawing.Point(260, 165),
                    Size = new System.Drawing.Size(80, 30),
                    DialogResult = DialogResult.OK,
                };
                var reject = new Button
                {
                    Text = "&Reject",
                    Location = new System.Drawing.Point(360, 165),
                    Size = new System.Drawing.Size(80, 30),
                    DialogResult = DialogResult.Cancel,
                };
                form.Controls.Add(label);
                form.Controls.Add(approve);
                form.Controls.Add(reject);
                form.AcceptButton = reject;
                form.CancelButton = reject;

                var timer = new System.Windows.Forms.Timer { Interval = TimeoutSeconds * 1000 };
                timer.Tick += (_, _) => { form.DialogResult = DialogResult.Cancel; form.Close(); };
                timer.Start();

                using var reg = ct.Register(() =>
                {
                    try { form.BeginInvoke(() => { form.DialogResult = DialogResult.Cancel; form.Close(); }); }
                    catch { }
                });

                var result = form.ShowDialog();
                timer.Stop();
                tcs.TrySetResult(result == DialogResult.OK);
            }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }
}
