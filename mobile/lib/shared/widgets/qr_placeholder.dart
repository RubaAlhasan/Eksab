import 'package:flutter/material.dart';

/// A deterministic QR-looking block.
///
/// The prototype draws a random dot grid purely for looks. This does the same
/// but derives the pattern from [seed], so a given wallet/coupon code always
/// renders the same image instead of flickering on every rebuild. Swap this for
/// a real encoder (e.g. `qr_flutter`) once the backend issues scan tokens —
/// nothing else needs to change, the widget's API is already `seed` + `size`.
class QrPlaceholder extends StatelessWidget {
  const QrPlaceholder({
    super.key,
    required this.seed,
    this.size = 240,
    this.modules = 8,
    this.padding = 20,
    this.background = const Color(0xFF0F172A),
    this.foreground = Colors.white,
    this.borderRadius = 24,
  });

  final String seed;
  final double size;

  /// Grid resolution — the prototype uses 6, 7, and 8 depending on the screen.
  final int modules;
  final double padding;
  final Color background;
  final Color foreground;
  final double borderRadius;

  @override
  Widget build(BuildContext context) {
    final cells = _pattern(seed, modules);
    final gap = 4.0;
    final cellSize =
        (size - padding * 2 - gap * (modules - 1)) / modules;

    return Container(
      width: size,
      height: size,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(borderRadius),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (var row = 0; row < modules; row++) ...[
            if (row > 0) SizedBox(height: gap),
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                for (var col = 0; col < modules; col++) ...[
                  if (col > 0) SizedBox(width: gap),
                  Container(
                    width: cellSize,
                    height: cellSize,
                    decoration: BoxDecoration(
                      color: cells[row * modules + col]
                          ? foreground
                          : Colors.transparent,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ],
              ],
            ),
          ],
        ],
      ),
    );
  }

  /// Cheap deterministic hash → bit per cell. Not a real QR encoding.
  static List<bool> _pattern(String seed, int modules) {
    var hash = 0x811c9dc5;
    for (final unit in seed.codeUnits) {
      hash = (hash ^ unit) * 0x01000193 & 0xFFFFFFFF;
    }
    return List.generate(modules * modules, (i) {
      hash = (hash * 1664525 + 1013904223) & 0xFFFFFFFF;
      return (hash >> 16) % 100 > 45;
    });
  }
}
