using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppClubCalculatoare
{
    /// <summary>
    /// Bază pentru formularele care încarcă date din SQL: un singur
    /// CancellationTokenSource activ, indicator de așteptare și tratarea
    /// centralizată a erorilor. Toate operațiile pornesc de pe UI-thread
    /// și revin pe UI-thread după await.
    /// </summary>
    public class AsyncFormBase : Form
    {
        private CancellationTokenSource _ctsOperatie;
        private int _operatiiActive;

        /// <summary>Anulează operația anterioară și începe una nouă.</summary>
        protected CancellationToken ÎncepeOperatie()
        {
            _ctsOperatie?.Cancel();
            _ctsOperatie?.Dispose();
            _ctsOperatie = new CancellationTokenSource();

            _operatiiActive++;
            if (!IsDisposed) UseWaitCursor = true;
            return _ctsOperatie.Token;
        }

        protected void TerminaOperatie()
        {
            if (_operatiiActive > 0) _operatiiActive--;
            if (_operatiiActive == 0 && !IsDisposed) UseWaitCursor = false;
        }

        /// <summary>
        /// Rulează o operație async cu busy-cursor, anulare la o operație nouă
        /// și mesaj de eroare doar dacă formularul mai există.
        /// </summary>
        protected async Task RunOperationAsync(
            Func<CancellationToken, Task> corp,
            string titluEroare = "Eroare")
        {
            CancellationToken ct = ÎncepeOperatie();
            try
            {
                await corp(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Închidere formular sau reîncărcare — nu este o eroare.
            }
            catch (Exception ex)
            {
                AppLogger.Error(ex, GetType().Name);
                if (!IsDisposed && !ct.IsCancellationRequested)
                {
                    MessageBox.Show(
                        "Eroare: " + ex.Message,
                        titluEroare, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                TerminaOperatie();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _ctsOperatie?.Cancel();
            _ctsOperatie?.Dispose();
            _ctsOperatie = null;
            base.OnFormClosed(e);
        }
    }
}
