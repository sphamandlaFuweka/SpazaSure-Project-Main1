import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/constants/app_text_styles.dart';
import 'package:spazasure_app/features/customer/screens/customer_rewards_screen.dart';
import 'package:spazasure_app/features/customer/screens/customer_scan_history_screen.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/services/customer_profile_service.dart';

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

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final firstName = _profile?.firstName ?? auth.firstName;
    final lastName = _profile?.lastName ?? auth.lastName;
    final fullName = [firstName, lastName].where((s) => s.isNotEmpty).join(' ');
    final displayName = fullName.isNotEmpty
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
      backgroundColor: const Color(0xFFF0F4F0),
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
          colors: [Color(0xFF0D3B0F), Color(0xFF1B5E20), Color(0xFF2E7D32)],
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
              Container(
                width: 90,
                height: 90,
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
                child: Center(
                  child: Text(
                    initials,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 32,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ),
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
      title: 'Account details',
      children: [
        _infoTile(
          Icons.badge_outlined,
          'First name',
          firstName.isNotEmpty ? firstName : '—',
        ),
        _infoTile(
          Icons.badge_outlined,
          'Last name',
          lastName.isNotEmpty ? lastName : '—',
        ),
        _infoTile(
          Icons.email_outlined,
          'Email',
          email.isNotEmpty ? email : '—',
        ),
        _infoTile(
          Icons.phone_outlined,
          'Phone',
          phone.isNotEmpty ? phone : '—',
        ),
        _infoTile(Icons.cake_outlined, 'Age', age != null ? '$age' : '—'),
      ],
    );
  }

  Widget _buildAllergiesSection(List<String> allergies) {
    return _sectionCard(
      title: 'Declared allergies',
      subtitle:
          'Every product you scan is checked against these automatically.',
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 4, 16, 16),
          child: allergies.isEmpty
              ? const Text(
                  'None declared',
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
      title: 'Activity',
      children: [
        ListTile(
          leading: const Icon(Icons.history_rounded, color: AppColors.primary),
          title: const Text('Scan history'),
          subtitle: const Text('Everything you\'ve scanned and reported'),
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
          title: const Text('My rewards'),
          subtitle: const Text('Points, vouchers, and redemptions'),
          trailing: const Icon(Icons.chevron_right_rounded),
          onTap: () => Navigator.push(
            context,
            MaterialPageRoute(builder: (_) => const CustomerRewardsScreen()),
          ),
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
          label: const Text('Sign out'),
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
