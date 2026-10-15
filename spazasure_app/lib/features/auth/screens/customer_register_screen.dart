import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/address_autocomplete_field.dart';
import 'package:spazasure_app/services/address_service.dart';
import 'package:spazasure_app/providers/auth_provider.dart';

class CustomerRegisterScreen extends StatefulWidget {
  const CustomerRegisterScreen({super.key});

  @override
  State<CustomerRegisterScreen> createState() => _CustomerRegisterScreenState();
}

class _CustomerRegisterScreenState extends State<CustomerRegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _firstNameController = TextEditingController();
  final _lastNameController = TextEditingController();
  final _preferredNameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  final _otherLanguageController = TextEditingController();
  final _cityController = TextEditingController();
  final _suburbController = TextEditingController();

  /// "Find your area" search; picking a result fills province, city, suburb and the map point
  final _areaController = TextEditingController();
  bool _areaConfirmed = false;
  final _ageController = TextEditingController();
  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;
  bool _loading = false;
  bool _gettingLocation = false;
  bool _nearbyRecommendations = false;
  bool _marketingOptIn = false;
  double? _latitude;
  double? _longitude;
  String _language = 'English';
  String? _province;

  static const _languages = [
    'English',
    'isiZulu',
    'isiXhosa',
    'Sesotho',
    'Setswana',
    'Sepedi',
    'Xitsonga',
    'Tshivenda',
    'siSwati',
    'isiNdebele',
    'Afrikaans',
    'Other',
  ];

  static const _provinces = [
    'KwaZulu-Natal',
    'Gauteng',
    'Eastern Cape',
    'Western Cape',
    'Limpopo',
    'Mpumalanga',
    'Free State',
    'North West',
    'Northern Cape',
  ];

  static const _interests = [
    'Scan products and check product information',
    'Check product safety alerts',
    'Report suspicious products',
    'Find nearby spaza shops',
    'Discover promotions and bulk-buy deals',
    'Access cashback and rewards',
    'Compare products and prices',
  ];

  static const _notificationOptions = [
    'Important product safety notifications',
    'Scan and report updates',
    'Cashback and reward updates',
    'Deals and promotions',
  ];

  static const _allergyOptions = [
    'Peanuts',
    'Tree nuts',
    'Dairy',
    'Eggs',
    'Gluten/Wheat',
    'Soy',
    'Fish',
    'Shellfish',
    'Sesame',
  ];

  final Set<String> _selectedInterests = {};
  final Set<String> _selectedNotifications = {};
  final Set<String> _selectedAllergies = {};

  @override
  void dispose() {
    _firstNameController.dispose();
    _lastNameController.dispose();
    _preferredNameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    _otherLanguageController.dispose();
    _cityController.dispose();
    _suburbController.dispose();
    _areaController.dispose();
    _ageController.dispose();
    super.dispose();
  }

  String? _validatePhone(String? value) {
    final digits = (value ?? '').replaceAll(RegExp(r'\D'), '');
    final valid =
        (digits.length == 9 && RegExp(r'^[678]\d{8}$').hasMatch(digits)) ||
        (digits.length == 10 && digits.startsWith('0')) ||
        (digits.length == 11 && digits.startsWith('27'));
    return valid ? null : 'Enter a valid South African mobile number';
  }

  String? _validateEmail(String? value) {
    final email = (value ?? '').trim();
    if (email.isEmpty) return null;
    return RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(email)
        ? null
        : 'Enter a valid email address';
  }

  void _applyArea(AddressSuggestion place) {
    setState(() {
      _areaConfirmed = true;
      final province = _provinces.cast<String?>().firstWhere(
        (p) => p!.toLowerCase() == place.province.toLowerCase(),
        orElse: () => null,
      );
      if (province != null) _province = province;
      if (place.city.isNotEmpty) _cityController.text = place.city;
      if (place.suburb.isNotEmpty) _suburbController.text = place.suburb;
      _latitude = place.latitude;
      _longitude = place.longitude;
    });
  }

  Future<void> _useDeviceLocation() async {
    setState(() => _gettingLocation = true);
    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        throw StateError('Turn on location services and try again.');
      }

      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied) {
        throw StateError(
          'Location permission was not granted. You can enter your area manually.',
        );
      }
      if (permission == LocationPermission.deniedForever) {
        throw StateError(
          'Location access is blocked in settings. You can enter your area manually.',
        );
      }

      final position = await Geolocator.getCurrentPosition();
      if (!mounted) return;
      setState(() {
        _latitude = position.latitude;
        _longitude = position.longitude;
        _nearbyRecommendations = true;
      });
      // Also fill in the area names for this spot
      try {
        final place = await AddressService.reverse(
          position.latitude,
          position.longitude,
        );
        if (place != null && mounted) {
          _areaController.text = [
            place.suburb,
            place.city,
          ].where((p) => p.isNotEmpty).join(', ');
          _applyArea(place);
        }
      } catch (_) {
        // The coordinates are enough for nearby recommendations
      }
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Your location is ready to use.')),
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(error.toString()),
          backgroundColor: AppColors.error,
        ),
      );
    } finally {
      if (mounted) setState(() => _gettingLocation = false);
    }
  }

  Future<void> _continue() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;

    setState(() => _loading = true);
    try {
      await context.read<AuthProvider>().registerCustomer(
        phone: _phoneController.text.trim(),
        firstName: _firstNameController.text.trim(),
        lastName: _lastNameController.text.trim(),
        password: _passwordController.text,
        email: _emailController.text.trim(),
        preferredName: _preferredNameController.text.trim(),
        preferredLanguage: _language == 'Other'
            ? _otherLanguageController.text.trim()
            : _language,
        province: _province,
        city: _cityController.text.trim(),
        suburb: _suburbController.text.trim(),
        nearbyRecommendationsEnabled: _nearbyRecommendations,
        latitude: _latitude,
        longitude: _longitude,
        age: _ageController.text.trim().isEmpty
            ? null
            : int.tryParse(_ageController.text.trim()),
        allergies: _selectedAllergies.toList(),
        interests: _selectedInterests.toList(),
        notificationPreferences: _selectedNotifications.toList(),
        marketingOptIn: _marketingOptIn,
      );
      if (!mounted) return;
      Navigator.pushNamedAndRemoveUntil(
        context,
        '/customer-home',
        (route) => false,
      );
    } catch (error) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(error.toString()),
          backgroundColor: AppColors.error,
        ),
      );
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Create customer account')),
      body: Form(
        key: _formKey,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(18, 12, 18, 32),
          children: [
            _buildIntro(),
            const SizedBox(height: 20),
            _buildSection(
              number: 'A',
              title: 'Personal information',
              subtitle: 'Tell us how to address you and how to reach you.',
              icon: Icons.person_outline_rounded,
              children: [
                _textField(
                  controller: _firstNameController,
                  label: 'First name',
                  required: true,
                  capitalization: TextCapitalization.words,
                  validator: (v) =>
                      (v ?? '').trim().isEmpty ? 'Enter your first name' : null,
                ),
                const SizedBox(height: 14),
                _textField(
                  controller: _lastNameController,
                  label: 'Surname',
                  required: true,
                  capitalization: TextCapitalization.words,
                  validator: (v) =>
                      (v ?? '').trim().isEmpty ? 'Enter your surname' : null,
                ),
                const SizedBox(height: 14),
                _textField(
                  controller: _preferredNameController,
                  label: 'Preferred name',
                  helper: 'Optional',
                  capitalization: TextCapitalization.words,
                ),
                const SizedBox(height: 14),
                _textField(
                  controller: _phoneController,
                  label: 'Mobile number',
                  required: true,
                  hint: '82 123 4567',
                  prefix: '+27',
                  keyboardType: TextInputType.phone,
                  validator: _validatePhone,
                ),
                const SizedBox(height: 6),
                Text(
                  'Enter 9 digits after +27, or use the local format starting with 0.',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 14),
                _textField(
                  controller: _emailController,
                  label: 'Email address',
                  helper: 'Optional, recommended for account updates',
                  keyboardType: TextInputType.emailAddress,
                  validator: _validateEmail,
                ),
                const SizedBox(height: 14),
                DropdownButtonFormField<String>(
                  initialValue: _language,
                  decoration: const InputDecoration(
                    labelText: 'Preferred language *',
                    prefixIcon: Icon(Icons.translate_rounded),
                  ),
                  items: _languages
                      .map(
                        (language) => DropdownMenuItem(
                          value: language,
                          child: Text(language),
                        ),
                      )
                      .toList(),
                  onChanged: (language) {
                    if (language != null) setState(() => _language = language);
                  },
                ),
                if (_language == 'Other') ...[
                  const SizedBox(height: 14),
                  _textField(
                    controller: _otherLanguageController,
                    label: 'Your preferred language',
                    required: true,
                    validator: (value) => (value ?? '').trim().isEmpty
                        ? 'Enter a language'
                        : null,
                  ),
                ],
              ],
            ),
            const SizedBox(height: 14),
            _buildSection(
              number: 'B',
              title: 'Account security',
              subtitle: 'Sign in with your mobile number and password.',
              icon: Icons.lock_outline_rounded,
              children: [
                _textField(
                  controller: _passwordController,
                  label: 'Create password',
                  required: true,
                  obscure: _obscurePassword,
                  suffix: IconButton(
                    tooltip: _obscurePassword
                        ? 'Show password'
                        : 'Hide password',
                    onPressed: () =>
                        setState(() => _obscurePassword = !_obscurePassword),
                    icon: Icon(
                      _obscurePassword
                          ? Icons.visibility_outlined
                          : Icons.visibility_off_outlined,
                    ),
                  ),
                  validator: (value) => (value ?? '').length < 8
                      ? 'Use at least 8 characters'
                      : null,
                ),
                const SizedBox(height: 14),
                _textField(
                  controller: _confirmPasswordController,
                  label: 'Confirm password',
                  required: true,
                  obscure: _obscureConfirmPassword,
                  suffix: IconButton(
                    tooltip: _obscureConfirmPassword
                        ? 'Show password'
                        : 'Hide password',
                    onPressed: () => setState(
                      () => _obscureConfirmPassword = !_obscureConfirmPassword,
                    ),
                    icon: Icon(
                      _obscureConfirmPassword
                          ? Icons.visibility_outlined
                          : Icons.visibility_off_outlined,
                    ),
                  ),
                  validator: (value) => value != _passwordController.text
                      ? 'Passwords do not match'
                      : null,
                ),
                const SizedBox(height: 10),
                _hintBox(
                  Icons.info_outline_rounded,
                  'We are using phone and password sign-in. SMS verification codes are not required.',
                ),
              ],
            ),
            const SizedBox(height: 14),
            _buildSection(
              number: 'C',
              title: 'Location preferences',
              subtitle:
                  'Optional. Choose your area or use your current location.',
              icon: Icons.location_on_outlined,
              children: [
                AddressAutocompleteField(
                  controller: _areaController,
                  label: 'Find your area',
                  hint: 'Start typing your suburb, township or town',
                  confirmed: _areaConfirmed,
                  onSelected: _applyArea,
                  onEdited: () {
                    if (_areaConfirmed) setState(() => _areaConfirmed = false);
                  },
                ),
                const SizedBox(height: 14),
                DropdownButtonFormField<String>(
                  // Rebuild when a picked area sets the province
                  key: ValueKey(_province),
                  initialValue: _province,
                  decoration: const InputDecoration(
                    labelText: 'Province',
                    prefixIcon: Icon(Icons.map_outlined),
                  ),
                  hint: const Text('Choose a province'),
                  items: _provinces
                      .map(
                        (province) => DropdownMenuItem(
                          value: province,
                          child: Text(province),
                        ),
                      )
                      .toList(),
                  onChanged: (province) => setState(() => _province = province),
                ),
                const SizedBox(height: 14),
                _textField(
                  controller: _cityController,
                  label: 'City or municipality',
                  capitalization: TextCapitalization.words,
                ),
                const SizedBox(height: 14),
                _textField(
                  controller: _suburbController,
                  label: 'Suburb or township',
                  capitalization: TextCapitalization.words,
                ),
                const SizedBox(height: 8),
                if (_latitude != null && _longitude != null) ...[
                  _hintBox(
                    Icons.my_location_rounded,
                    'Current location added. Nearby recommendations are enabled.',
                    color: AppColors.success,
                  ),
                  const SizedBox(height: 8),
                ],
                OutlinedButton.icon(
                  onPressed: _gettingLocation ? null : _useDeviceLocation,
                  icon: _gettingLocation
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.my_location_rounded),
                  label: Text(
                    _gettingLocation
                        ? 'Getting your location...'
                        : _latitude == null
                        ? 'Use my current location'
                        : 'Update current location',
                  ),
                ),
                Material(
                  color: AppColors.surface,
                  child: CheckboxListTile(
                    value: _nearbyRecommendations,
                    contentPadding: EdgeInsets.zero,
                    controlAffinity: ListTileControlAffinity.leading,
                    title: const Text('Enable nearby shop recommendations'),
                    subtitle: const Text(
                      'You can use your current location or enter your area above.',
                    ),
                    onChanged: (value) => setState(() {
                      _nearbyRecommendations = value ?? false;
                      if (!_nearbyRecommendations) {
                        _latitude = null;
                        _longitude = null;
                      }
                    }),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            _buildSection(
              number: 'D',
              title: 'Interests and updates',
              subtitle: 'Choose what you want to use and hear about.',
              icon: Icons.tune_rounded,
              children: [
                _subheading('What would you like to use SpazaSure for?'),
                const SizedBox(height: 4),
                ..._interests.map(
                  (interest) => _checkOption(
                    interest,
                    _selectedInterests.contains(interest),
                    (selected) =>
                        _toggle(_selectedInterests, interest, selected),
                  ),
                ),
                const SizedBox(height: 14),
                _subheading('Service notifications'),
                const SizedBox(height: 4),
                Text(
                  'Choose service and safety updates separately from marketing.',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 4),
                ..._notificationOptions.map(
                  (preference) => _checkOption(
                    preference,
                    _selectedNotifications.contains(preference),
                    (selected) =>
                        _toggle(_selectedNotifications, preference, selected),
                  ),
                ),
                const Divider(height: 24),
                _checkOption(
                  'I would like to receive SpazaSure offers, promotions and marketing messages.',
                  _marketingOptIn,
                  (selected) => setState(() => _marketingOptIn = selected),
                ),
                const SizedBox(height: 14),
                _subheading('Food allergies (optional)'),
                const SizedBox(height: 4),
                Text(
                  'We use these to help highlight relevant product information when you scan.',
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: AppColors.textSecondary,
                  ),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 4,
                  children: _allergyOptions
                      .map(
                        (allergy) => FilterChip(
                          label: Text(allergy),
                          selected: _selectedAllergies.contains(allergy),
                          onSelected: (selected) =>
                              _toggle(_selectedAllergies, allergy, selected),
                        ),
                      )
                      .toList(),
                ),
                const SizedBox(height: 14),
                _textField(
                  controller: _ageController,
                  label: 'Age',
                  helper: 'Optional',
                  keyboardType: TextInputType.number,
                  validator: (value) {
                    if ((value ?? '').trim().isEmpty) return null;
                    final age = int.tryParse(value!.trim());
                    return age == null || age < 18 || age > 130
                        ? 'You must be at least 18 to register'
                        : null;
                  },
                ),
              ],
            ),
            const SizedBox(height: 14),
            _hintBox(
              Icons.policy_outlined,
              'Terms and Privacy acknowledgements will be added when the finalized policy documents and links are ready.',
            ),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: _loading ? null : _continue,
              icon: _loading
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : const Icon(Icons.arrow_forward_rounded),
              label: Text(_loading ? 'Creating account...' : 'Create account'),
            ),
            const SizedBox(height: 8),
            Text(
              'Your account is protected by your mobile number and password.',
              textAlign: TextAlign.center,
              style: Theme.of(
                context,
              ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildIntro() {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [AppColors.primaryDark, AppColors.primary],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(22),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.14),
              borderRadius: BorderRadius.circular(14),
            ),
            child: const Icon(
              Icons.verified_user_outlined,
              color: Colors.white,
            ),
          ),
          const SizedBox(height: 14),
          const Text(
            'Shop with confidence',
            style: TextStyle(
              color: Colors.white,
              fontSize: 24,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'Create your account to check products, find local shops and get useful safety updates.',
            style: TextStyle(color: Colors.white70, height: 1.4),
          ),
          const SizedBox(height: 14),
          const Row(
            children: [
              Icon(Icons.check_circle_outline, size: 17, color: Colors.white),
              SizedBox(width: 7),
              Expanded(
                child: Text(
                  'Required fields are marked with *',
                  style: TextStyle(color: Colors.white, fontSize: 12),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildSection({
    required String number,
    required String title,
    required String subtitle,
    required IconData icon,
    required List<Widget> children,
  }) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.divider),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.09),
                  borderRadius: BorderRadius.circular(13),
                ),
                child: Icon(icon, color: AppColors.primary),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'SECTION $number',
                      style: Theme.of(context).textTheme.labelSmall?.copyWith(
                        color: AppColors.primary,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 0.8,
                      ),
                    ),
                    Text(
                      title,
                      style: Theme.of(context).textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            subtitle,
            style: Theme.of(
              context,
            ).textTheme.bodySmall?.copyWith(color: AppColors.textSecondary),
          ),
          const SizedBox(height: 16),
          ...children,
        ],
      ),
    );
  }

  Widget _textField({
    required TextEditingController controller,
    required String label,
    bool required = false,
    String? helper,
    String? hint,
    String? prefix,
    bool obscure = false,
    Widget? suffix,
    TextInputType? keyboardType,
    TextCapitalization capitalization = TextCapitalization.none,
    String? Function(String?)? validator,
  }) {
    return TextFormField(
      controller: controller,
      obscureText: obscure,
      keyboardType: keyboardType,
      textCapitalization: capitalization,
      decoration: InputDecoration(
        labelText: required ? '$label *' : label,
        helperText: helper,
        hintText: hint,
        prefixText: prefix == null ? null : '$prefix ',
        prefixIcon: prefix == null ? null : const Icon(Icons.phone_rounded),
        suffixIcon: suffix,
      ),
      validator: validator,
    );
  }

  Widget _subheading(String text) => Text(
    text,
    style: Theme.of(
      context,
    ).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w800),
  );

  Widget _checkOption(
    String title,
    bool selected,
    ValueChanged<bool> onChanged,
  ) {
    return Material(
      color: AppColors.surface,
      child: CheckboxListTile(
        value: selected,
        contentPadding: EdgeInsets.zero,
        dense: true,
        controlAffinity: ListTileControlAffinity.leading,
        title: Text(title, style: Theme.of(context).textTheme.bodyMedium),
        onChanged: (value) => onChanged(value ?? false),
      ),
    );
  }

  Widget _hintBox(
    IconData icon,
    String text, {
    Color color = AppColors.primary,
  }) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.07),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: color),
          const SizedBox(width: 9),
          Expanded(
            child: Text(
              text,
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: AppColors.textPrimary,
                height: 1.35,
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _toggle(Set<String> values, String value, bool selected) {
    setState(() {
      if (selected) {
        values.add(value);
      } else {
        values.remove(value);
      }
    });
  }
}
