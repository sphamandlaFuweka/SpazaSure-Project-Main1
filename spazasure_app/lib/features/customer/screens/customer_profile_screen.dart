import 'package:spazasure_app/core/widgets/privacy_widgets.dart';
import 'package:spazasure_app/services/privacy_service.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/core/widgets/editable_avatar.dart';
import 'package:spazasure_app/features/customer/screens/customer_rewards_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_scan_history_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_subscription_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/services/customer_profile_service.dart';
import 'package:spazasure_app/core/i18n/app_language.dart';
import 'package:spazasure_app/core/widgets/language_picker.dart';

/// The customer role's own Profile tab — deliberately distinct from the
/// shop-owner ProfileScreen: a customer isn't running a shop, so this shows
/// personal details (name, email, phone, age) and declared allergies
/// instead of shop/compliance fields.
class CustomerProfileScreen extends StatefulWidget {
  const CustomerProfileScreen({super.key});

  @override
  State<CustomerProfileScreen> createState() => _CustomerProfileScreenState();
}

class _CustomerProfileScreenState extends State<CustomerProfileScreen> {
  CustomerProfile? _profile;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      final profile = await CustomerProfileService.getProfile();
      if (mounted) setState(() => _profile = profile);
    } catch (_) {
      // Fall back to whatever the session already has cached.
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _showEdit() async {
    final profile = _profile;
    final auth = context.read<AuthProvider>();
    final first = TextEditingController(
      text: profile?.firstName ?? auth.firstName,
    );
    final last = TextEditingController(
      text: profile?.lastName ?? auth.lastName,
    );
    final email = TextEditingController(text: profile?.email ?? auth.email);
    final age = TextEditingController(
      text: (profile?.age ?? auth.age)?.toString() ?? '',
    );
    final allergies = TextEditingController(
      text: (profile?.allergies ?? auth.allergies).join(', '),
    );
    final formKey = GlobalKey<FormState>();
    var saving = false;

    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setSheet) => Padding(
          padding: EdgeInsets.fromLTRB(
            20,
            20,
            20,
            MediaQuery.of(ctx).viewInsets.bottom + 20,
          ),
          child: Form(
            key: formKey,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(tr('profile.edit'), style: AppTextStyles.h3),
                  const SizedBox(height: 16),
                  TextFormField(
                    controller: first,
                    textCapitalization: TextCapitalization.words,
                    decoration: InputDecoration(labelText: tr('profile.firstName')),
                    validator: (v) => (v ?? '').trim().isEmpty
                        ? 'Enter your first name'
                        : null,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: last,
                    textCapitalization: TextCapitalization.words,
                    decoration: InputDecoration(labelText: tr('profile.lastName')),
                    validator: (v) => (v ?? '').trim().isEmpty
                        ? 'Enter your last name'
                        : null,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: email,
                    keyboardType: TextInputType.emailAddress,
                    decoration: InputDecoration(labelText: tr('profile.email')),
                    validator: (v) {
                      final t = (v ?? '').trim();
                      if (t.isEmpty) return null;
                      return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(t)
                          ? null
                          : 'Enter a valid email';
                    },
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: age,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(labelText: 'Age'),
                    validator: (v) {
                      final t = (v ?? '').trim();
                      if (t.isEmpty) return null;
                      final n = int.tryParse(t);
                      return n == null || n < 0 || n > 130
                          ? 'Enter a valid age'
                          : null;
                    },
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: allergies,
                    decoration: const InputDecoration(
                      labelText: 'Allergies',
                      helperText: 'Separate with commas, e.g. peanuts, milk',
                    ),
                  ),
                  const SizedBox(height: 20),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: saving
                          ? null
                          : () async {
                              if (!formKey.currentState!.validate()) return;
                              setSheet(() => saving = true);
                              try {
                                final list = allergies.text
                                    .split(',')
                                    .map((a) => a.trim().toLowerCase())
                                    .where((a) => a.isNotEmpty)
                                    .toSet()
                                    .toList();
                                await CustomerProfileService.updateProfile(
                                  firstName: first.text.trim(),
                                  lastName: last.text.trim(),
                                  email: email.text.trim(),
                                  age: int.tryParse(age.text.trim()),
                                  allergies: list,
                                );
                                if (ctx.mounted) Navigator.pop(ctx, true);
                              } catch (e) {
                                setSheet(() => saving = false);
                                if (ctx.mounted) {
                                  ScaffoldMessenger.of(ctx).showSnackBar(
                                    SnackBar(
                                      content: Text(e.toString()),
                                      backgroundColor: AppColors.error,
                                    ),
                                  );
                                }
                              }
                            },
                      child: Text(saving ? 'Saving...' : 'Save changes'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );

    first.dispose();
    last.dispose();
    email.dispose();
    age.dispose();
    allergies.dispose();

    if (saved == true) {
      await _load();
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Profile saved'),
          backgroundColor: AppColors.success,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final firstName = _profile?.firstName ?? auth.firstName;
    final lastName = _profile?.lastName ?? auth.lastName;
    final fullName = [firstName, lastName].where((s) => s.isNotEmpty).join(' ');
    final preferredName = _profile?.preferredName ?? auth.preferredName ?? '';
    final displayName = preferredName.trim().isNotEmpty
        ? preferredName.trim()
        : fullName.isNotEmpty
        ? fullName
        : (auth.fullName.isNotEmpty ? auth.fullName : 'Customer');
    final email = _profile?.email ?? auth.email;
    final phone = _profile?.phone ?? auth.phone;
    final age = _profile?.age ?? auth.age;
    final allergies = _profile?.allergies ?? auth.allergies;
    final initials = displayName.trim().isNotEmpty
        ? displayName
              .trim()
              .split(' ')
              .where((w) => w.isNotEmpty)
              .take(2)
              .map((w) => w[0].toUpperCase())
              .join()
        : 'C';

    return Scaffold(
      backgroundColor: const Color(0xFFF5F7FF),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: _load,
              child: CustomScrollView(
                physics: const AlwaysScrollableScrollPhysics(),
                slivers: [
                  SliverToBoxAdapter(
                    child: _buildHeader(initials, displayName, phone),
                  ),
                  SliverToBoxAdapter(
                    child: _buildAccountSection(
                      firstName,
                      lastName,
                      email,
                      phone,
                      age,
                    ),
                  ),
                  SliverToBoxAdapter(child: _buildAllergiesSection(allergies)),
                  SliverToBoxAdapter(child: _buildMenuSection()),
                  const SliverToBoxAdapter(
                    child: Padding(
                      padding: EdgeInsets.symmetric(horizontal: 16),
                      child: PrivacySection(
                        service: PrivacyService.customer,
                        deletionExplanation:
                            'We will erase your name, age, contact details, location and preferences, and '
                            'remove your name from reports and reviews. Reward records are kept without '
                            'your details. A Plus subscription that renews must be cancelled first. '
                            'This cannot be undone once done.',
                      ),
                    ),
                  ),
                  SliverToBoxAdapter(child: _buildLogoutButton()),
                  const SliverToBoxAdapter(child: SizedBox(height: 32)),
                ],
              ),
            ),
    );
  }

  Widget _buildHeader(String initials, String displayName, String phone) {
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
              Row(
                children: [
                  IconButton(
                    tooltip: tr('profile.edit'),
                    onPressed: _showEdit,
                    icon: const Icon(
                      Icons.edit_rounded,
                      color: Colors.white70,
                      size: 22,
                    ),
                  ),
                  const Spacer(),
                  Text(
                    tr('profile.title'),
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const Spacer(),
                  IconButton(
                    onPressed: _load,
                    icon: const Icon(
                      Icons.refresh_rounded,
                      color: Colors.white70,
                      size: 22,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 20),
              EditableAvatar(
                initials: initials,
                photoUrl: _profile?.profilePhotoUrl,
                uploadPath: '/customer/profile/photo',
                onUploaded: (_) => _load(),
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
              if (phone.isNotEmpty) ...[
                const SizedBox(height: 6),
                Text(
                  phone,
                  style: const TextStyle(color: Colors.white60, fontSize: 12),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildAccountSection(
    String firstName,
    String lastName,
    String email,
    String phone,
    int? age,
  ) {
    return _sectionCard(
      title: tr('profile.account'),
      children: [
        _infoTile(
          Icons.badge_rounded,
          tr('profile.firstName'),
          firstName.isNotEmpty ? firstName : '—',
        ),
        _infoTile(
          Icons.badge_rounded,
          tr('profile.lastName'),
          lastName.isNotEmpty ? lastName : '—',
        ),
        _infoTile(Icons.email_rounded, tr('profile.email'), email.isNotEmpty ? email : '—'),
        _infoTile(Icons.phone_rounded, tr('profile.phone'), phone.isNotEmpty ? phone : '—'),
        _infoTile(Icons.cake_rounded, 'Age', age != null ? '$age' : '—'),
      ],
    );
  }

  Widget _buildAllergiesSection(List<String> allergies) {
    return _sectionCard(
      title: tr('profile.allergies'),
      subtitle:
          'Every product you scan is checked against these automatically.',
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
          child: allergies.isEmpty
              ? Text(
                  tr('profile.noAllergies'),
                  style: TextStyle(color: AppColors.textHint),
                )
              : Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: allergies
                      .map(
                        (a) => Chip(
                          label: Text(a),
                          backgroundColor: AppColors.primary.withValues(
                            alpha: 0.12,
                          ),
                        ),
                      )
                      .toList(),
                ),
        ),
      ],
    );
  }

  Widget _buildMenuSection() {
    return _sectionCard(
      title: tr('profile.activity'),
      children: [
        ListTile(
          leading: const Icon(
            Icons.workspace_premium_rounded,
            color: AppColors.primary,
          ),
          title: Text(tr('profile.membership')),
          subtitle: Text(tr('profile.membershipSub')),
          trailing: const Icon(Icons.chevron_right_rounded),
          onTap: () => Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => const CustomerSubscriptionScreen(),
            ),
          ),
        ),
        ListTile(
          leading: const Icon(Icons.history_rounded, color: AppColors.primary),
          title: Text(tr('profile.scanHistory')),
          subtitle: Text(tr('profile.scanHistorySub')),
          trailing: const Icon(Icons.chevron_right_rounded),
          onTap: () => Navigator.push(
            context,
            MaterialPageRoute(
              builder: (_) => const CustomerScanHistoryScreen(),
            ),
          ),
        ),
        ListTile(
          leading: const Icon(Icons.redeem_rounded, color: AppColors.primary),
          title: Text(tr('home.rewards')),
          subtitle: Text(tr('profile.rewardsSub')),
          trailing: const Icon(Icons.chevron_right_rounded),
          onTap: () => Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => const CustomerRewardsScreen()),
          ),
        ),
        // Also saved as the customer's preferred language, so it follows them to a new phone
        LanguagePickerTile(
          onChanged: (code) => CustomerProfileService.updateProfile(
            preferredLanguage: AppLanguage.supported[code],
          ).catchError((_) {}),
        ),
      ],
    );
  }

  Widget _buildLogoutButton() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 0),
      child: SizedBox(
        width: double.infinity,
        child: OutlinedButton.icon(
          onPressed: () async {
            await context.read<AuthProvider>().logout();
            if (!mounted) return;
            Navigator.pushNamedAndRemoveUntil(context, '/login', (r) => false);
          },
          icon: const Icon(Icons.logout_rounded),
          label: Text(tr('profile.signOut')),
          style: OutlinedButton.styleFrom(
            foregroundColor: AppColors.error,
            side: const BorderSide(color: AppColors.error),
          ),
        ),
      ),
    );
  }

  Widget _sectionCard({
    required String title,
    String? subtitle,
    required List<Widget> children,
  }) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
      child: Container(
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(20),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: AppTextStyles.subtitle),
                  if (subtitle != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      subtitle,
                      style: AppTextStyles.caption.copyWith(
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ],
              ),
            ),
            ...children,
          ],
        ),
      ),
    );
  }

  Widget _infoTile(IconData icon, String label, String value) {
    return ListTile(
      leading: Icon(icon, color: AppColors.textSecondary),
      title: Text(
        label,
        style: AppTextStyles.caption.copyWith(color: AppColors.textSecondary),
      ),
      subtitle: Text(value, style: AppTextStyles.body),
    );
  }
}
