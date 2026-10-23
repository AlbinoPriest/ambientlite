# USB protocol notes

Interoperability notes for the tested controller; similarly branded strips may use other protocols.

## Device

- Vendor `0x1A86`, product `0xFE07`, HID interface `00`.
- 65-byte output report, including report ID zero.
- Exactly one matching controller is required.

The keyboard interface of the composite device is not used. No driver replacement is required.

## Colour frame

| Field | Length | Value |
|---|---:|---|
| Header | 2 | ASCII `SC` (`53 43`) |
| Total frame length | 2 | Unsigned 16-bit big-endian, including checksum |
| Sequence | 1 | Cycles through `1`–`254` |
| Command | 1 | `0x80` |
| Records | 5 × count | Start LED, R, G, B, end LED |
| Checksum | 1 | Sum of preceding bytes modulo 256 |

LED indexes are one-based; ranges are inclusive. AmbientLite sends one record per LED, with equal start/end indexes. The packet supports 1–254 LEDs; the UI requires a positive top count and two equal positive side counts.

Split a frame into chunks of at most 64 bytes. Each HID output report starts with `0x00`, followed by a chunk, zero-padded to 65 bytes total.

The self-test sets LED 1 red and LED 2 blue, sequence 2:

```text
53 43 00 11 02 80 01 FF 00 00 01 02 00 00 FF 02 2D
```

## Behaviour

Normal operation sends colour frames. Brightness scales RGB values in software; static colours use the same format as screen sync. No firmware-update or persistent controller-configuration commands are sent.

Pausing closes the HID connection and leaves the last colour displayed. Static mode updates on settings changes; screen sync refreshes an unchanged frame after approximately one second.

The format was identified by inspecting interoperability logic in locally installed SyncLight 2.18.1. No vendor source code or native binaries are redistributed here.

Windows references: [HID capabilities](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidpi/ns-hidpi-_hidp_caps), [HID API](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/introduction-to-hid-concepts), [StretchBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-stretchblt).
