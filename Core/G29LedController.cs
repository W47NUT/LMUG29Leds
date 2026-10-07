using HidSharp;
using System.IO;

namespace LMUG29Leds.Core;

internal sealed class G29LedController : IDisposable
{
    private const int LogitechVendorId = 0x046D;
    private const int G29ProductId = 0xC24F;
    private const int ExpectedOutputReportLength = 17;
    private const byte ValidLedBits = 0x1F;

    private readonly object _sync = new();
    private HidStream? _stream;

    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _stream is not null;
            }
        }
    }

    public bool TryConnect()
    {
        lock (_sync)
        {
            if (_stream is not null)
            {
                return true;
            }

            foreach (HidDevice device in DeviceList.Local.GetHidDevices(
                         LogitechVendorId,
                         G29ProductId))
            {
                try
                {
                    if (device.GetMaxOutputReportLength() != ExpectedOutputReportLength)
                    {
                        continue;
                    }

                    if (!device.TryOpen(out HidStream stream))
                    {
                        continue;
                    }

                    try
                    {
                        WriteMask(stream, 0x00);
                        _stream = stream;
                        return true;
                    }
                    catch (IOException)
                    {
                        stream.Dispose();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        stream.Dispose();
                    }
                }
                catch (IOException)
                {
                    // USB devices can disappear during reconnects.
                }
                catch (UnauthorizedAccessException)
                {
                    // Never elevate; another process may own this interface.
                }
            }

            return false;
        }
    }

    public bool TrySetMask(byte mask)
    {
        lock (_sync)
        {
            if (_stream is null)
            {
                return false;
            }

            try
            {
                WriteMask(_stream, (byte)(mask & ValidLedBits));
                return true;
            }
            catch (IOException)
            {
                DisconnectUnsafe();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                DisconnectUnsafe();
                return false;
            }
            catch (ObjectDisposedException)
            {
                DisconnectUnsafe();
                return false;
            }
        }
    }

    public void Disconnect()
    {
        lock (_sync)
        {
            DisconnectUnsafe();
        }
    }

    public void Dispose()
    {
        Disconnect();
    }

    private static void WriteMask(HidStream stream, byte mask)
    {
        byte[] report = new byte[ExpectedOutputReportLength];

        report[0] = 0x00;
        report[1] = 0xF8;
        report[2] = 0x12;
        report[3] = (byte)(mask & ValidLedBits);
        report[4] = 0x00;
        report[5] = 0x00;
        report[6] = 0x00;
        report[7] = 0x01;

        stream.Write(report);
    }

    private void DisconnectUnsafe()
    {
        if (_stream is null)
        {
            return;
        }

        try
        {
            WriteMask(_stream, 0x00);
        }
        catch
        {
            // Best effort only during shutdown/reconnect.
        }

        _stream.Dispose();
        _stream = null;
    }
}
