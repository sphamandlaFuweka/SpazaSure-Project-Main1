import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/services/api_service.dart';

/// Round avatar showing the profile picture (or initials) that uploads a new one when tapped.
class EditableAvatar extends StatefulWidget {
  final String initials;
  final String? photoUrl;
  final String uploadPath;
  final double size;
  final ValueChanged<String> onUploaded;

  const EditableAvatar({
    super.key,
    required this.initials,
    required this.photoUrl,
    required this.uploadPath,
    required this.onUploaded,
    this.size = 90,
  });

  @override
  State<EditableAvatar> createState() => _EditableAvatarState();
}

class _EditableAvatarState extends State<EditableAvatar> {
  bool _uploading = false;

  Future<void> _choose() async {
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Wrap(
          children: [
            ListTile(
              leading: const Icon(Icons.photo_camera_rounded),
              title: const Text('Take a photo'),
              onTap: () => Navigator.pop(ctx, ImageSource.camera),
            ),
            ListTile(
              leading: const Icon(Icons.photo_library_rounded),
              title: const Text('Choose from gallery'),
              onTap: () => Navigator.pop(ctx, ImageSource.gallery),
            ),
          ],
        ),
      ),
    );
    if (source == null) return;

    final picked = await ImagePicker().pickImage(
      source: source,
      maxWidth: 1024,
      maxHeight: 1024,
      imageQuality: 85,
    );
    if (picked == null) return;

    setState(() => _uploading = true);
    try {
      final bytes = await picked.readAsBytes();
      final name = picked.name.contains('.')
          ? picked.name
          : '${picked.name}.jpg';
      final res = await ApiService.uploadFile(
        widget.uploadPath,
        fieldName: 'file',
        fileBytes: bytes,
        fileName: name,
      );
      final url = (res['data'] as Map?)?['profilePhotoUrl']?.toString();
      if (url == null || url.isEmpty)
        throw ApiException('No picture URL returned.', 0);
      if (!mounted) return;
      widget.onUploaded(url);
      _toast('Profile picture updated', AppColors.success);
    } catch (e) {
      if (mounted) _toast(e.toString(), AppColors.error);
    } finally {
      if (mounted) setState(() => _uploading = false);
    }
  }

  void _toast(String message, Color color) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: color,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final url = ApiService.resolveUploadUrl(widget.photoUrl);
    return Semantics(
      button: true,
      label: 'Change profile picture',
      child: GestureDetector(
        onTap: _uploading ? null : _choose,
        child: SizedBox(
          width: widget.size,
          height: widget.size,
          child: Stack(
            clipBehavior: Clip.none,
            children: [
              Container(
                width: widget.size,
                height: widget.size,
                clipBehavior: Clip.antiAlias,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  gradient: const LinearGradient(
                    colors: [Color(0xFF4CAF50), Color(0xFF81C784)],
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black.withValues(alpha: 0.3),
                      blurRadius: 16,
                      offset: const Offset(0, 6),
                    ),
                  ],
                ),
                child: url == null
                    ? _initials()
                    : Image.network(
                        url,
                        fit: BoxFit.cover,
                        errorBuilder: (_, __, ___) => _initials(),
                      ),
              ),
              if (_uploading)
                Positioned.fill(
                  child: DecoratedBox(
                    decoration: const BoxDecoration(
                      shape: BoxShape.circle,
                      color: Colors.black45,
                    ),
                    child: const Center(
                      child: SizedBox(
                        width: 26,
                        height: 26,
                        child: CircularProgressIndicator(
                          strokeWidth: 3,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ),
                ),
              Positioned(
                right: -2,
                bottom: -2,
                child: Container(
                  width: 30,
                  height: 30,
                  decoration: BoxDecoration(
                    color: AppColors.accent,
                    shape: BoxShape.circle,
                    border: Border.all(color: Colors.white, width: 2),
                  ),
                  child: const Icon(
                    Icons.camera_alt_rounded,
                    size: 15,
                    color: Colors.white,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _initials() => Center(
    child: Text(
      widget.initials,
      style: TextStyle(
        color: Colors.white,
        fontSize: widget.size * 0.36,
        fontWeight: FontWeight.w800,
      ),
    ),
  );
}
