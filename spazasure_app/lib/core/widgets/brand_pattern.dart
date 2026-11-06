import 'package:flutter/material.dart';

/// SpazaSure's brand pattern, for every blue surface: `BoxDecoration(image: brandPatternImage)`.
/// The artwork is white on transparent, so it sits over any colour, and stays faint so it
/// never competes with the text on top of it.
const brandPatternImage = DecorationImage(
  image: AssetImage('assets/images/brand_pattern.png'),
  fit: BoxFit.cover,
  opacity: 0.04,
);
