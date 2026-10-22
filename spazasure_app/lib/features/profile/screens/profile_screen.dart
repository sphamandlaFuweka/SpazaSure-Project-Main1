import 'package:spazasure_app/core/widgets/privacy_widgets.dart';
import 'package:spazasure_app/services/privacy_service.dart';
import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/address_autocomplete_field.dart';
import 'package:spazasure_app/core/widgets/editable_avatar.dart';
import 'package:spazasure_app/features/profile/screens/onboarding_fee_checkout_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/services/profile_service.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';
import 'package:spazasure_app/core/widgets/language_picker.dart';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  ShopProfile? _profile;
  bool _loading = true;
  bool _startingPayment = false;

  @override
  void initState() {
    super.initState();
    _loadProfile();
  }

  Future<void> _loadProfile() async {
    setState(() => _loading = true);
    try {
      final profile = await ProfileService.getProfile();
      if (mounted) {
        setState(() {
          _profile = profile;
          _loading = false;
        });
      }
    } catch (_) {
      // Show profile UI even if API fails (user might not be logged in yet)
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final ownerName = _profile?.ownerName ?? auth.session?.fullName ?? '';
    final shopName =
        _profile?.shopName ?? auth.session?.shopName ?? 'My Spaza Shop';
    final phone = _profile?.phone ?? auth.session?.phone ?? '';
    final email = _profile?.email ?? '';
    final address = _profile?.address ?? '';
    final city = _profile?.city ?? '';
    final province = _profile?.province ?? '';
    final latitude = _profile?.latitude;
    final longitude = _profile?.longitude;
    final status = _profile?.status ?? 'pending';
    final complianceStatus = _profile?.complianceStatus ?? 'incomplete';
    final ratingAvg = _profile?.ratingAvg ?? 0.0;
    final ratingCount = _profile?.ratingCount ?? 0;
    final onboardingFeePaid = _profile?.onboardingFeePaid ?? false;
    final onboardingFeeAmount = _profile?.onboardingFeeAmount ?? 150;

    final displayName = ownerName.isNotEmpty ? ownerName : shopName;
    final initials = displayName.isNotEmpty
        ? displayName
              .split(' ')
              .where((w) => w.isNotEmpty)
              .take(2)
              .map((w) => w[0].toUpperCase())
              .join()
        : 'SP';

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FF),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _loadProfile,
              child: CustomScrollView(
                physics: const AlwaysScrollableScrollPhysics(),
                slivers: [
                  // Premium gradient header
                  SliverToBoxAdapter(
                    child: _buildHeader(
                      initials,
                      displayName,
                      shopName,
                      ownerName,
                      phone,
                      email,
                      status,
                    ),
                  ),
                  // Stats cards
                  SliverToBoxAdapter(
                    child: _buildStatsCards(
                      ratingAvg,
                      ratingCount,
                      complianceStatus,
                      onboardingFeePaid,
                    ),
                  ),
                  // Menu sections
                  SliverToBoxAdapter(
                    child: _buildAccountSection(
                      shopName,
                      ownerName,
                      phone,
                      email,
                      address,
                      city,
                      province,
                      latitude,
                      longitude,
                    ),
                  ),
                  SliverToBoxAdapter(
                    child: _buildShopSection(
                      address,
                      city,
                      province,
                      onboardingFeePaid,
                      onboardingFeeAmount,
                    ),
                  ),
                  SliverToBoxAdapter(child: _buildAppSection()),
                  const SliverToBoxAdapter(
                    child: Padding(
                      padding: EdgeInsets.symmetric(horizontal: 16),
                      child: PrivacySection(
                        service: PrivacyService.shop,
                        deletionExplanation:
                            'We will erase your name, ID number, contact details, shop address and documents, '
                            'and remove your shop from SpazaSure. Order and payment records are kept for 5 years '
                            'without your details, as tax law requires. Money in your wallet and unfinished '
                            'orders must be settled first. This cannot be undone once done.',
                      ),
                    ),
                  ),
                  SliverToBoxAdapter(child: _buildLogoutButton()),
                  SliverToBoxAdapter(child: _buildFooter()),
                ],
              ),
            ),
    );
  }

  Widget _buildHeader(
    String initials,
    String displayName,
    String shopName,
    String ownerName,
    String phone,
    String email,
    String status,
  ) {
    final isVerified = status == 'verified' || status == 'active';
    return Container(
      decoration: const BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [Color(0xFF1A3278), Color(0xFF25449A), Color(0xFF3F5DB0)],
        ),
        borderRadius: BorderRadius.vertical(bottom: Radius.circular(32)),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(20, 12, 20, 28),
          child: Column(
            children: [
              // Top bar
              Row(
                children: [
                  const SizedBox(width: 48),
                  const Spacer(),
                  const Text(
                    'My Profile',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const Spacer(),
                  IconButton(
                    onPressed: _loadProfile,
                    icon: const Icon(
                      Icons.refresh_rounded,
                      color: Colors.white70,
                      size: 22,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 20),
              // Avatar
              EditableAvatar(
                initials: initials,
                photoUrl: _profile?.profilePhotoUrl,
                uploadPath: '/shop/profile/photo',
                onUploaded: (_) => _loadProfile(),
              ),
              const SizedBox(height: 14),
              Text(
                displayName,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                ),
              ),
              if (ownerName.isNotEmpty && shopName != ownerName)
                Text(
                  shopName,
                  style: const TextStyle(color: Colors.white70, fontSize: 13),
                ),
              const SizedBox(height: 6),
              if (phone.isNotEmpty)
                Text(
                  phone,
                  style: const TextStyle(color: Colors.white60, fontSize: 12),
                ),
              const SizedBox(height: 10),
              // Status badge
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 14,
                  vertical: 6,
                ),
                decoration: BoxDecoration(
                  color: isVerified
                      ? Colors.white.withValues(alpha: 0.2)
                      : const Color(0xFFF8B217).withValues(alpha: 0.25),
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(
                    color: isVerified
                        ? const Color(0xFF4DD898)
                        : const Color(0xFFFFD54F),
                    width: 1,
                  ),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(
                      isVerified ? Icons.verified : Icons.pending_outlined,
                      size: 16,
                      color: isVerified
                          ? const Color(0xFF4DD898)
                          : const Color(0xFFFFD54F),
                    ),
                    const SizedBox(width: 6),
                    Text(
                      isVerified ? 'Verified Account' : 'Pending Verification',
                      style: TextStyle(
                        color: isVerified
                            ? const Color(0xFF4DD898)
                            : const Color(0xFFFFD54F),
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    ).animate().fadeIn(duration: 400.ms);
  }

  Widget _buildStatsCards(
    double ratingAvg,
    int ratingCount,
    String complianceStatus,
    bool onboardingFeePaid,
  ) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
      child: Row(
        children: [
          _statCard(
            '⭐',
            ratingAvg > 0 ? ratingAvg.toStringAsFixed(1) : 'New',
            ratingCount == 1 ? '1 ${tr('shop.review')}' : '$ratingCount ${tr('shop.reviews')}',
            const Color(0xFFFFA726),
            () => _showReviews(context),
          ),
          const SizedBox(width: 10),
          _statCard(
            '🛡️',
            _complianceLabel(complianceStatus),
            tr('shop.complianceStat'),
            _complianceColor(complianceStatus),
            _openCompliance,
          ),
          const SizedBox(width: 10),
          _statCard(
            '💳',
            onboardingFeePaid ? tr('shop.paid') : tr('shop.due'),
            tr('shop.onboardingStat'),
            onboardingFeePaid ? AppColors.success : AppColors.error,
            () {
              if (onboardingFeePaid || _startingPayment) {
                _showVerificationStatus(context);
              } else {
                _payOnboardingFee();
              }
            },
          ),
        ],
      ),
    ).animate().fadeIn(delay: 100.ms).slideY(begin: 0.05, end: 0);
  }

  Widget _statCard(
    String emoji,
    String value,
    String label,
    Color color,
    VoidCallback onTap,
  ) {
    return Expanded(
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          onTap: onTap,
          borderRadius: BorderRadius.circular(18),
          child: Ink(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(18),
              boxShadow: [
                BoxShadow(
                  color: color.withValues(alpha: 0.12),
                  blurRadius: 12,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
            child: Column(
              children: [
                Text(emoji, style: const TextStyle(fontSize: 22)),
                const SizedBox(height: 6),
                Text(
                  value,
                  style: TextStyle(
                    color: color,
                    fontSize: 15,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  label,
                  style: AppTextStyles.caption.copyWith(fontSize: 10),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  /// Documents decide the compliance status, so refresh it when the owner comes back.
  Future<void> _openCompliance() async {
    await Navigator.pushNamed(context, '/compliance');
    if (mounted) _loadProfile();
  }

  Widget _buildAccountSection(
    String shopName,
    String ownerName,
    String phone,
    String email,
    String address,
    String city,
    String province,
    double? latitude,
    double? longitude,
  ) {
    return _menuSection(tr('shop.account'), Icons.person_outline, [
      _menuTile(
        Icons.store_rounded,
        tr('shop.details'),
        tr('shop.detailsSub'),
        AppColors.primary,
        () => _showShopDetails(
          context,
          shopName,
          ownerName,
          phone,
          email,
          address,
          city,
          province,
          latitude,
          longitude,
        ),
      ),
      _menuTile(
        Icons.verified_rounded,
        tr('shop.sticker'),
        tr('shop.stickerSub'),
        const Color(0xFF078C50),
        () => Navigator.pushNamed(context, '/my-sticker'),
      ),
      _menuTile(
        Icons.account_balance_wallet_rounded,
        tr('shop.wallet'),
        tr('shop.walletSub'),
        const Color(0xFF2196F3),
        () => Navigator.pushNamed(context, '/wallet'),
      ),
      _menuTile(
        Icons.shopping_bag_rounded,
        tr('shop.orders'),
        tr('shop.ordersSub'),
        const Color(0xFF9C27B0),
        () => Navigator.pushNamed(context, '/orders'),
      ),
    ]).animate().fadeIn(delay: 150.ms).slideY(begin: 0.03, end: 0);
  }

  Widget _buildShopSection(
    String address,
    String city,
    String province,
    bool onboardingFeePaid,
    double onboardingFeeAmount,
  ) {
    return _menuSection(tr('shop.compliance'), Icons.shield_outlined, [
      _menuTile(
        Icons.description_rounded,
        tr('shop.documents'),
        tr('shop.documentsSub'),
        const Color(0xFFFF9800),
        _openCompliance,
      ),
      _menuTile(
        Icons.verified_user_rounded,
        tr('shop.verification'),
        tr('shop.verificationSub'),
        AppColors.success,
        () => _showVerificationStatus(context),
      ),
      _menuTile(
        onboardingFeePaid ? Icons.check_circle : Icons.payments_outlined,
        onboardingFeePaid ? 'Onboarding Fee Paid' : 'Pay Onboarding Fee',
        onboardingFeePaid
            ? 'Payment confirmed — tap to view status'
            : _startingPayment
            ? 'Starting payment... — tap to view status'
            : 'R${onboardingFeeAmount.toStringAsFixed(2)} once-off',
        onboardingFeePaid ? AppColors.success : AppColors.error,
        () {
          if (onboardingFeePaid || _startingPayment) {
            _showVerificationStatus(context);
          } else {
            _payOnboardingFee();
          }
        },
      ),
      _menuTile(
        Icons.qr_code_scanner_rounded,
        tr('shop.scan'),
        tr('shop.scanSub'),
        const Color(0xFF00BCD4),
        () => Navigator.pushNamed(context, '/qr-scanner'),
      ),
    ]).animate().fadeIn(delay: 200.ms).slideY(begin: 0.03, end: 0);
  }

  Widget _buildAppSection() {
    return _menuSection(tr('shop.app'), Icons.settings_outlined, [
      _menuTile(
        Icons.notifications_rounded,
        tr('shop.notifications'),
        tr('shop.notificationsSub'),
        const Color(0xFFE91E63),
        () => Navigator.pushNamed(context, '/notifications'),
      ),
      _menuTile(
        Icons.headset_mic_rounded,
        tr('shop.help'),
        tr('shop.helpSub'),
        const Color(0xFF607D8B),
        () => _showHelpSupport(context),
      ),
      _menuTile(
        Icons.info_rounded,
        tr('shop.about'),
        tr('shop.aboutSub'),
        const Color(0xFF795548),
        () => _showAbout(context),
      ),
      const LanguagePickerTile(),
    ]).animate().fadeIn(delay: 250.ms).slideY(begin: 0.03, end: 0);
  }

  Widget _menuSection(String title, IconData titleIcon, List<Widget> items) {
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(20),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.04),
            blurRadius: 10,
            offset: const Offset(0, 3),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(18, 16, 18, 4),
            child: Row(
              children: [
                Icon(titleIcon, size: 16, color: AppColors.textHint),
                const SizedBox(width: 6),
                Text(
                  title.toUpperCase(),
                  style: AppTextStyles.caption.copyWith(
                    fontWeight: FontWeight.w700,
                    letterSpacing: 0.8,
                    color: AppColors.textHint,
                  ),
                ),
              ],
            ),
          ),
          ...items,
          const SizedBox(height: 8),
        ],
      ),
    );
  }

  Widget _menuTile(
    IconData icon,
    String title,
    String subtitle,
    Color color,
    VoidCallback onTap,
  ) {
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(14),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          child: Row(
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(icon, color: color, size: 20),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: AppTextStyles.body.copyWith(
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                    Text(
                      subtitle,
                      style: AppTextStyles.caption.copyWith(
                        fontSize: 11,
                        color: AppColors.textHint,
                      ),
                    ),
                  ],
                ),
              ),
              Container(
                width: 28,
                height: 28,
                decoration: BoxDecoration(
                  color: const Color(0xFFF5F5F5),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: const Icon(
                  Icons.chevron_right_rounded,
                  color: AppColors.textHint,
                  size: 18,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildLogoutButton() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
      child: GestureDetector(
        onTap: () async {
          final navigator = Navigator.of(context);
          await context.read<AuthProvider>().logout();
          if (!mounted) return;
          navigator.pushNamedAndRemoveUntil('/login', (route) => false);
        },
        child: Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(vertical: 16),
          decoration: BoxDecoration(
            color: AppColors.error.withValues(alpha: 0.08),
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: AppColors.error.withValues(alpha: 0.2)),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Icon(Icons.logout_rounded, color: AppColors.error, size: 20),
              const SizedBox(width: 10),
              Text(
                'Log Out',
                style: AppTextStyles.body.copyWith(
                  color: AppColors.error,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
      ),
    ).animate().fadeIn(delay: 300.ms);
  }

  Widget _buildFooter() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 24, 16, 40),
      child: Column(
        children: [
          Text(
            'SpazaSure v1.0.0',
            style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
          ),
          const SizedBox(height: 4),
          Text(
            'Made with ❤️ in South Africa',
            style: AppTextStyles.caption.copyWith(
              color: AppColors.textHint,
              fontSize: 10,
            ),
          ),
        ],
      ),
    );
  }

  // ── Helpers ──
  String _complianceLabel(String status) {
    switch (status) {
      case 'green':
        return 'Good';
      case 'orange':
        return 'Review';
      case 'red':
        return 'Action';
      default:
        return 'N/A';
    }
  }

  Color _complianceColor(String status) {
    switch (status) {
      case 'green':
        return AppColors.success;
      case 'orange':
        return AppColors.warning;
      case 'red':
        return AppColors.error;
      default:
        return AppColors.textHint;
    }
  }

  // ── Shop Details Bottom Sheet ──
  void _showShopDetails(
    BuildContext context,
    String shopName,
    String ownerName,
    String phone,
    String email,
    String address,
    String city,
    String province,
    double? latitude,
    double? longitude,
  ) {
    final shopCtrl = TextEditingController(text: shopName);
    final ownerCtrl = TextEditingController(text: ownerName);
    final phoneCtrl = TextEditingController(text: phone);
    final emailCtrl = TextEditingController(text: email);
    final addressCtrl = TextEditingController(text: address);
    final cityCtrl = TextEditingController(text: city);
    final provinceCtrl = TextEditingController(text: province);
    final latitudeCtrl = TextEditingController(
      text: latitude?.toString() ?? '',
    );
    final longitudeCtrl = TextEditingController(
      text: longitude?.toString() ?? '',
    );
    var addressConfirmed = latitude != null && longitude != null;
    var locationPicked = false;
    var saving = false;
    String? postalCode;

    Future<void> saveProfile(
      BuildContext sheetContext,
      StateSetter setSaveState,
    ) async {
      if (shopCtrl.text.trim().isEmpty ||
          ownerCtrl.text.trim().isEmpty ||
          phoneCtrl.text.trim().isEmpty ||
          addressCtrl.text.trim().isEmpty ||
          cityCtrl.text.trim().isEmpty ||
          provinceCtrl.text.trim().isEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Complete all shop and contact fields before saving.',
            ),
            backgroundColor: AppColors.error,
          ),
        );
        return;
      }
      if (addressCtrl.text != address && !addressConfirmed) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text(
              'Pick your address from the suggestions to confirm it.',
            ),
            backgroundColor: AppColors.error,
            behavior: SnackBarBehavior.floating,
          ),
        );
        return;
      }

      setSaveState(() => saving = true);
      try {
        final locationChanged =
            addressCtrl.text.trim() != address.trim() ||
            cityCtrl.text.trim() != city.trim() ||
            provinceCtrl.text.trim() != province.trim();
        final keepSelectedLocation = !locationChanged || locationPicked;
        await ProfileService.updateProfile({
          'shopName': shopCtrl.text.trim(),
          'ownerName': ownerCtrl.text.trim(),
          'phone': phoneCtrl.text.trim(),
          'email': emailCtrl.text.trim(),
          'address': addressCtrl.text.trim(),
          'city': cityCtrl.text.trim(),
          'province': provinceCtrl.text.trim(),
          if (postalCode != null) 'postalCode': postalCode,
          'latitude': !keepSelectedLocation || latitudeCtrl.text.trim().isEmpty
              ? null
              : double.tryParse(latitudeCtrl.text.trim()),
          'longitude':
              !keepSelectedLocation || longitudeCtrl.text.trim().isEmpty
              ? null
              : double.tryParse(longitudeCtrl.text.trim()),
        });
        if (sheetContext.mounted) Navigator.pop(sheetContext);
        await _loadProfile();
        if (!mounted) return;
        ScaffoldMessenger.of(this.context).showSnackBar(
          const SnackBar(
            content: Text('Profile updated!'),
            backgroundColor: AppColors.success,
            behavior: SnackBarBehavior.floating,
          ),
        );
      } catch (e) {
        if (!mounted) return;
        ScaffoldMessenger.of(this.context).showSnackBar(
          SnackBar(
            content: Text(
              'Could not save your profile: ${e.toString().replaceFirst('Exception: ', '')}',
            ),
            backgroundColor: AppColors.error,
            behavior: SnackBarBehavior.floating,
          ),
        );
      } finally {
        if (sheetContext.mounted) setSaveState(() => saving = false);
      }
    }

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (ctx) => DraggableScrollableSheet(
        initialChildSize: 0.85,
        maxChildSize: 0.95,
        minChildSize: 0.5,
        builder: (_, controller) => Container(
          decoration: const BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
          ),
          child: Column(
            children: [
              const SizedBox(height: 12),
              Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.divider,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
              Padding(
                padding: const EdgeInsets.all(20),
                child: Row(
                  children: [
                    Container(
                      width: 40,
                      height: 40,
                      decoration: BoxDecoration(
                        color: AppColors.primary.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(12),
                      ),
                      child: const Icon(
                        Icons.store_rounded,
                        color: AppColors.primary,
                        size: 20,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Text(tr('shop.details'), style: AppTextStyles.h3),
                    const Spacer(),
                  ],
                ),
              ),
              Expanded(
                child: ListView(
                  controller: controller,
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  children: [
                    _editField('Shop Name', shopCtrl, Icons.store_outlined),
                    _editField('Owner Name', ownerCtrl, Icons.person_outline),
                    _editField(
                      'Phone',
                      phoneCtrl,
                      Icons.phone_outlined,
                      type: TextInputType.phone,
                    ),
                    _editField(
                      'Email',
                      emailCtrl,
                      Icons.email_outlined,
                      type: TextInputType.emailAddress,
                    ),
                    StatefulBuilder(
                      builder: (_, setFieldState) => Padding(
                        padding: const EdgeInsets.only(bottom: 14),
                        child: AddressAutocompleteField(
                          controller: addressCtrl,
                          label: 'Address',
                          confirmed: addressConfirmed,
                          onSelected: (s) => setFieldState(() {
                            locationPicked = true;
                            addressConfirmed = true;
                            addressCtrl.text = s.addressLine.isEmpty
                                ? s.label
                                : s.addressLine;
                            cityCtrl.text = s.city;
                            provinceCtrl.text = s.province;
                            postalCode = s.postalCode;
                            latitudeCtrl.text = s.latitude.toString();
                            longitudeCtrl.text = s.longitude.toString();
                          }),
                          onEdited: () {
                            if (addressConfirmed) {
                              setFieldState(() => addressConfirmed = false);
                            }
                          },
                        ),
                      ),
                    ),
                    _editField('City', cityCtrl, Icons.location_city_outlined),
                    _editField('Province', provinceCtrl, Icons.map_outlined),
                    const SizedBox(height: 40),
                  ],
                ),
              ),
              StatefulBuilder(
                builder: (context, setSaveState) => SafeArea(
                  top: false,
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(20, 12, 20, 16),
                    child: SizedBox(
                      width: double.infinity,
                      height: 52,
                      child: ElevatedButton.icon(
                        onPressed: saving
                            ? null
                            : () => saveProfile(ctx, setSaveState),
                        icon: saving
                            ? const SizedBox(
                                width: 18,
                                height: 18,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : const Icon(Icons.save_outlined),
                        label: Text(saving ? 'Saving...' : 'Save changes'),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppColors.primary,
                          foregroundColor: Colors.white,
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14),
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _editField(
    String label,
    TextEditingController ctrl,
    IconData icon, {
    TextInputType? type,
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: TextField(
        controller: ctrl,
        keyboardType: type,
        style: GoogleFonts.nunito(fontSize: 14, color: const Color(0xFF1A1A1A)),
        decoration: InputDecoration(
          labelText: label,
          labelStyle: GoogleFonts.nunito(
            color: AppColors.textSecondary,
            fontSize: 12,
          ),
          prefixIcon: Icon(icon, color: AppColors.primary, size: 20),
          filled: true,
          fillColor: const Color(0xFFF8F9FA),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: BorderSide.none,
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(14),
            borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
          ),
        ),
      ),
    );
  }

  // ── Reviews ──
  void _showReviews(BuildContext context) {
    final future = ProfileService.getReviews();
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (ctx) => DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.7,
        maxChildSize: 0.92,
        builder: (ctx, controller) => Container(
          decoration: const BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
          ),
          child: FutureBuilder<ShopReviews>(
            future: future,
            builder: (ctx, snap) {
              final children = <Widget>[
                Center(
                  child: Container(
                    width: 40,
                    height: 4,
                    decoration: BoxDecoration(
                      color: AppColors.divider,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ),
                const SizedBox(height: 20),
                Text('Customer reviews', style: AppTextStyles.h3),
                const SizedBox(height: 16),
              ];
              final data = snap.data;
              if (snap.connectionState != ConnectionState.done) {
                children.add(
                  const Padding(
                    padding: EdgeInsets.all(32),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                );
              } else if (snap.hasError || data == null) {
                children.add(
                  Text(
                    'Could not load your reviews. Check your connection and try again.',
                    style: AppTextStyles.bodySmall,
                  ),
                );
              } else {
                children
                  ..add(_reviewSummary(data))
                  ..add(const SizedBox(height: 20));
                if (data.reviews.isEmpty) {
                  children.add(
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 24),
                      child: Text(
                        'No reviews yet. Customers can rate your shop in the SpazaSure customer app.',
                        textAlign: TextAlign.center,
                        style: AppTextStyles.bodySmall,
                      ),
                    ),
                  );
                } else {
                  children.addAll(data.reviews.map(_reviewTile));
                }
              }
              return ListView(
                controller: controller,
                padding: const EdgeInsets.all(24),
                children: children,
              );
            },
          ),
        ),
      ),
    );
  }

  Widget _reviewSummary(ShopReviews data) {
    final maxCount = data.breakdown.values.fold<int>(
      0,
      (a, b) => b > a ? b : a,
    );
    return Row(
      children: [
        Column(
          children: [
            Text(
              data.count == 0 ? '-' : data.average.toStringAsFixed(1),
              style: const TextStyle(fontSize: 36, fontWeight: FontWeight.w800),
            ),
            _stars(data.average.round()),
            const SizedBox(height: 4),
            Text(
              data.count == 1 ? '1 review' : '${data.count} reviews',
              style: AppTextStyles.caption,
            ),
          ],
        ),
        const SizedBox(width: 20),
        Expanded(
          child: Column(
            children: [
              for (var stars = 5; stars >= 1; stars--)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    children: [
                      SizedBox(
                        width: 14,
                        child: Text('$stars', style: AppTextStyles.caption),
                      ),
                      const SizedBox(width: 6),
                      Expanded(
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(4),
                          child: LinearProgressIndicator(
                            value: maxCount == 0
                                ? 0
                                : (data.breakdown[stars] ?? 0) / maxCount,
                            minHeight: 6,
                            backgroundColor: AppColors.divider,
                            color: const Color(0xFFFFA726),
                          ),
                        ),
                      ),
                      const SizedBox(width: 6),
                      SizedBox(
                        width: 24,
                        child: Text(
                          '${data.breakdown[stars] ?? 0}',
                          textAlign: TextAlign.right,
                          style: AppTextStyles.caption,
                        ),
                      ),
                    ],
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _reviewTile(ShopReview review) {
    final date = review.date;
    final comment = review.comment?.trim() ?? '';
    final reply = review.ownerReply?.trim() ?? '';
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFF5F7FF),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              _stars(review.rating),
              const Spacer(),
              if (date != null)
                Text(
                  '${date.day.toString().padLeft(2, '0')}/'
                  '${date.month.toString().padLeft(2, '0')}/${date.year}',
                  style: AppTextStyles.caption,
                ),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            comment.isEmpty ? 'No comment left.' : comment,
            style: AppTextStyles.bodySmall.copyWith(
              color: comment.isEmpty ? AppColors.textHint : null,
            ),
          ),
          if (reply.isNotEmpty) ...[
            const SizedBox(height: 8),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text('Your reply: $reply', style: AppTextStyles.bodySmall),
            ),
          ],
          Align(
            alignment: Alignment.centerRight,
            child: TextButton.icon(
              onPressed: () => _replyToReview(review),
              icon: Icon(reply.isEmpty ? Icons.reply_rounded : Icons.edit_outlined, size: 16),
              label: Text(reply.isEmpty ? 'Reply' : 'Edit reply'),
            ),
          ),
        ],
      ),
    );
  }

  /// Customers see the owner's reply under the review, and the reviewer is told about it.
  Future<void> _replyToReview(ShopReview review) async {
    final controller = TextEditingController(text: review.ownerReply ?? '');
    final reply = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Reply to review'),
        content: TextField(
          controller: controller,
          maxLength: 1000,
          maxLines: 4,
          autofocus: true,
          decoration: const InputDecoration(
            hintText: 'Thank the customer or explain what you have fixed',
          ),
        ),
        actions: [
          if ((review.ownerReply ?? '').isNotEmpty)
            TextButton(onPressed: () => Navigator.pop(ctx, ''), child: const Text('Remove reply')),
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(ctx, controller.text.trim()), child: const Text('Post')),
        ],
      ),
    );
    controller.dispose();
    if (reply == null || !mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    try {
      await ProfileService.replyToReview(review.id, reply);
      if (!mounted) return;
      // Reopen the list so it shows the new reply
      Navigator.of(context).pop();
      _showReviews(context);
      messenger.showSnackBar(SnackBar(
        content: Text(reply.isEmpty ? 'Reply removed' : 'Reply posted'),
        backgroundColor: AppColors.success,
      ));
    } catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.toString()), backgroundColor: AppColors.error));
    }
  }

  Widget _stars(int rating) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      for (var i = 1; i <= 5; i++)
        Icon(
          i <= rating ? Icons.star_rounded : Icons.star_outline_rounded,
          size: 16,
          color: const Color(0xFFFFA726),
        ),
    ],
  );

  // ── Verification Status ──
  Future<void> _payOnboardingFee() async {
    if (_startingPayment) return;
    setState(() => _startingPayment = true);
    try {
      final checkoutUrl =
          await ProfileService.initiatePaystackOnboardingPayment();
      if (!mounted) return;
      if (checkoutUrl == null || checkoutUrl.isEmpty) {
        throw StateError('Paystack checkout is unavailable.');
      }
      await Navigator.push<bool>(
        context,
        MaterialPageRoute(
          builder: (_) => OnboardingFeeCheckoutScreen(
            checkoutUrl: checkoutUrl,
            paymentProvider: 'Paystack',
            title: 'Pay onboarding fee',
          ),
        ),
      );
      final status = await ProfileService.getOnboardingPaymentStatus();
      if (!mounted) return;
      await _loadProfile();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            status.paid
                ? 'Onboarding payment confirmed.'
                : 'Payment is still pending. Refresh after Paystack confirms it.',
          ),
          backgroundColor: status.paid ? AppColors.success : AppColors.warning,
        ),
      );
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(error.toString()),
            backgroundColor: AppColors.error,
          ),
        );
      }
    } finally {
      if (mounted) setState(() => _startingPayment = false);
    }
  }

  void _showVerificationStatus(BuildContext context) {
    final status = _profile?.status ?? 'pending';
    final complianceStatus = _profile?.complianceStatus ?? 'incomplete';
    final feePaid = _profile?.onboardingFeePaid ?? false;

    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      builder: (ctx) => Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
        ),
        padding: const EdgeInsets.all(24),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.divider,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
              const SizedBox(height: 20),
              Text(tr('shop.verification'), style: AppTextStyles.h3),
              const SizedBox(height: 20),
              _verifyRow(
                tr('shop.account'),
                status == 'verified' || status == 'active'
                    ? 'Verified'
                    : 'Pending',
                status == 'verified' || status == 'active'
                    ? AppColors.success
                    : AppColors.warning,
                status == 'verified' || status == 'active'
                    ? Icons.check_circle
                    : Icons.hourglass_top,
              ),
              const SizedBox(height: 10),
              _verifyRow(
                tr('shop.complianceStat'),
                _complianceLabel(complianceStatus),
                _complianceColor(complianceStatus),
                complianceStatus == 'green'
                    ? Icons.check_circle
                    : Icons.warning_amber,
              ),
              const SizedBox(height: 10),
              _verifyRow(
                'Onboarding Fee',
                feePaid ? 'Paid' : 'Not Paid',
                feePaid ? AppColors.success : AppColors.error,
                feePaid ? Icons.check_circle : Icons.cancel,
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                height: 48,
                child: ElevatedButton(
                  onPressed: () {
                    Navigator.pop(ctx);
                    if (!feePaid) _payOnboardingFee();
                  },
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                  child: Text(
                    feePaid
                        ? 'Close'
                        : 'Pay R${(_profile?.onboardingFeeAmount ?? 150).toStringAsFixed(2)}',
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _verifyRow(String label, String value, Color color, IconData icon) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(14),
      ),
      child: Row(
        children: [
          Icon(icon, color: color, size: 22),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              label,
              style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w500),
            ),
          ),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.15),
              borderRadius: BorderRadius.circular(20),
            ),
            child: Text(
              value,
              style: AppTextStyles.caption.copyWith(
                color: color,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }

  // ── Help & Support ──
  void _showHelpSupport(BuildContext context) {
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      builder: (ctx) => Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
        ),
        padding: const EdgeInsets.all(24),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.divider,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
              const SizedBox(height: 20),
              Text(tr('shop.help'), style: AppTextStyles.h3),
              const SizedBox(height: 20),
              _supportItem(
                Icons.email_rounded,
                'Email',
                'support@spazasure.co.za',
                const Color(0xFF2196F3),
              ),
              const SizedBox(height: 10),
              _supportItem(
                Icons.phone_rounded,
                'Call',
                '0800 123 456',
                const Color(0xFF078C50),
              ),
              const SizedBox(height: 10),
              _supportItem(
                Icons.chat_rounded,
                'WhatsApp',
                '+27 60 123 4567',
                const Color(0xFF25D366),
              ),
              const SizedBox(height: 10),
              _supportItem(
                Icons.access_time_rounded,
                'Hours',
                'Mon-Fri 8am-5pm',
                const Color(0xFF9C27B0),
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                height: 48,
                child: ElevatedButton(
                  onPressed: () => Navigator.pop(ctx),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                  child: const Text('Close'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _supportItem(IconData icon, String title, String value, Color color) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.06),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: color.withValues(alpha: 0.12)),
      ),
      child: Row(
        children: [
          Container(
            width: 38,
            height: 38,
            decoration: BoxDecoration(
              color: color.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Icon(icon, color: color, size: 18),
          ),
          const SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: AppTextStyles.caption.copyWith(
                  color: AppColors.textHint,
                ),
              ),
              Text(
                value,
                style: AppTextStyles.body.copyWith(fontWeight: FontWeight.w600),
              ),
            ],
          ),
        ],
      ),
    );
  }

  // ── About ──
  void _showAbout(BuildContext context) {
    showModalBottomSheet(
      context: context,
      backgroundColor: Colors.transparent,
      builder: (ctx) => Container(
        decoration: const BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.vertical(top: Radius.circular(28)),
        ),
        padding: const EdgeInsets.all(24),
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.divider,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
              const SizedBox(height: 20),
              Container(
                width: 70,
                height: 70,
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [Color(0xFF078C50), Color(0xFF3F5DB0)],
                  ),
                  shape: BoxShape.circle,
                ),
                child: const Icon(
                  Icons.store_rounded,
                  color: Colors.white,
                  size: 34,
                ),
              ),
              const SizedBox(height: 14),
              Text('SpazaSure', style: AppTextStyles.h3),
              const SizedBox(height: 4),
              Text('Version 1.0.0', style: AppTextStyles.caption),
              const SizedBox(height: 14),
              Text(
                'Connecting spaza shop owners with verified suppliers for safe, affordable products across South Africa.',
                style: AppTextStyles.body.copyWith(
                  color: AppColors.textSecondary,
                  height: 1.5,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: const Color(0xFFF8F9FA),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: Column(
                  children: [
                    _aboutRow('Platform', 'Flutter & .NET 9'),
                    _aboutRow('Database', 'PostgreSQL'),
                    _aboutRow('Built by', 'SpazaSure Team'),
                    _aboutRow('Year', '2026'),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                height: 48,
                child: ElevatedButton(
                  onPressed: () => Navigator.pop(ctx),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                  child: const Text('Close'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _aboutRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: AppTextStyles.caption.copyWith(color: AppColors.textHint),
          ),
          Text(
            value,
            style: AppTextStyles.bodySmall.copyWith(
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}
