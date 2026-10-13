import 'package:flutter/material.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';
import 'package:spazasure_app/core/constants/app_colors.dart';
import 'package:spazasure_app/core/widgets/address_autocomplete_field.dart';
import 'package:spazasure_app/providers/auth_provider.dart';
import 'package:spazasure_app/services/address_service.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  static const _stepLabels = [
    'Representative',
    'Shop',
    'Location',
    'Operations',
    'Review',
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
  static const _shopTypes = [
    'Spaza shop',
    'Convenience store',
    'Mini-market',
    'Informal trader',
    'Other',
  ];
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
  static const _productCategories = [
    'Groceries',
    'Fresh produce',
    'Beverages',
    'Personal care',
    'Household',
    'Baby products',
    'Snacks and confectionery',
    'Other',
  ];
  static const _stockSources = [
    'Wholesalers',
    'Direct from suppliers',
    'Local markets',
    'Cash-and-carry',
    'Other',
  ];
  static const _days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
  static const _interests = [
    'Wholesale ordering',
    'Supplier deals',
    'Digital payments',
    'Product safety updates',
    'Business advice',
    'Promotions and rewards',
  ];
  static const _communications = [
    'Account and service updates',
    'Product safety notices',
    'Order and delivery updates',
    'Business support',
  ];
  static const _docTypes = <String, String>{
    'shop_photo': 'Shopfront or shop interior photo',
    'business_permit': 'Business permit or trading licence',
    'health_cert': 'Health certificate (if applicable)',
    'lease': 'Lease or proof of premises (optional)',
    'id_document': 'Representative identity document',
  };

  final _pageController = PageController();
  final _formControllers = <String, TextEditingController>{
    for (final field in [
      'firstName',
      'surname',
      'email',
      'phone',
      'alternatePhone',
      'password',
      'confirmPassword',
      'shopName',
      'tradingName',
      'companyRegistrationNumber',
      'address',
      'province',
      'municipality',
      'city',
      'suburbTownship',
      'standNumber',
      'postalCode',
      'openingTime',
      'closingTime',
      'deliveryRadiusKm',
      'storageNotes',
      'averageDailyCustomers',
    ])
      field: TextEditingController(),
  };
  final _pickedDocs = <String, PlatformFile>{};
  final _selected = <String, Set<String>>{
    'operatingDays': {},
    'productCategories': {},
    'stockSources': {},
    'interests': {},
    'communicationPreferences': {},
  };

  int _currentStep = 0;
  bool _isLoading = false;
  bool _obscurePassword = true;
  bool _obscureConfirmPassword = true;
  bool _deliveryAvailable = false;
  bool _refrigerationAvailable = false;
  bool _registrationDeclaredAccurate = false;
  bool _marketingOptIn = false;
  String? _preferredLanguage;
  String? _shopType;
  String? _registrationStatus;
  String? _monthlyStockBudget;
  AddressSuggestion? _address;

  TextEditingController _controller(String key) => _formControllers[key]!;

  @override
  void dispose() {
    _pageController.dispose();
    for (final controller in _formControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  void _showMessage(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message), behavior: SnackBarBehavior.floating),
    );
  }

  bool _validateCurrentStep() {
    String value(String key) => _controller(key).text.trim();
    String? error;
    switch (_currentStep) {
      case 0:
        if (value('firstName').isEmpty || value('surname').isEmpty) {
          error = 'Enter the representative’s first name and surname.';
        } else if (value('phone').isEmpty) {
          error = 'Enter the representative’s mobile number.';
        } else if (_controller('password').text.length < 8) {
          error = 'Create a password with at least 8 characters.';
        } else if (_controller('password').text !=
            _controller('confirmPassword').text) {
          error = 'The passwords do not match.';
        } else if (value('email').isNotEmpty &&
            !RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$').hasMatch(value('email'))) {
          error = 'Enter a valid email address or leave it blank.';
        }
        break;
      case 1:
        if (value('shopName').isEmpty) {
          error = 'Enter the name customers know your shop by.';
        } else if (_shopType == null) {
          error = 'Choose the type of business.';
        } else if (_registrationStatus == null) {
          error = 'Choose the business registration status.';
        }
        break;
      case 2:
        if (value('address').isEmpty) {
          error = 'Enter your shop’s street address or select a suggestion.';
        } else if (value('city').isEmpty || value('province').isEmpty) {
          error = 'Enter the town or city and province for the shop.';
        }
        break;
      case 4:
        if (!_registrationDeclaredAccurate) {
          error = 'Confirm that the information provided is accurate.';
        }
        break;
    }
    if (error != null) {
      _showMessage(error);
      return false;
    }
    return true;
  }

  void _nextStep() {
    if (!_validateCurrentStep()) return;
    if (_currentStep < _stepLabels.length - 1) {
      setState(() => _currentStep++);
      _pageController.nextPage(duration: 300.ms, curve: Curves.easeOutCubic);
    } else {
      _submit();
    }
  }

  void _prevStep() {
    if (_currentStep == 0) {
      Navigator.pop(context);
      return;
    }
    setState(() => _currentStep--);
    _pageController.previousPage(duration: 300.ms, curve: Curves.easeOutCubic);
  }

  Map<String, dynamic> _registrationDetails() => {
    'representativeFirstName': _controller('firstName').text.trim(),
    'representativeSurname': _controller('surname').text.trim(),
    'representativeRole': _representativeRole,
    'alternatePhone': _controller('alternatePhone').text.trim(),
    'preferredLanguage': _preferredLanguage,
    'tradingName': _controller('tradingName').text.trim(),
    'shopType': _shopType,
    'businessRegistrationStatus': _registrationStatus,
    'companyRegistrationNumber': _controller(
      'companyRegistrationNumber',
    ).text.trim(),
    'municipality': _controller('municipality').text.trim(),
    'suburbOrTownship': _controller('suburbTownship').text.trim(),
    'standOrUnitNumber': _controller('standNumber').text.trim(),
    'operatingDays': _selected['operatingDays']!.toList()..sort(),
    'openingTime': _controller('openingTime').text.trim(),
    'closingTime': _controller('closingTime').text.trim(),
    'deliveryAvailable': _deliveryAvailable,
    'deliveryRadiusKm': _controller('deliveryRadiusKm').text.trim(),
    'refrigerationAvailable': _refrigerationAvailable,
    'storageNotes': _controller('storageNotes').text.trim(),
    'averageDailyCustomers': _controller('averageDailyCustomers').text.trim(),
    'monthlyStockBudget': _monthlyStockBudget,
    'productCategories': _selected['productCategories']!.toList()..sort(),
    'stockSources': _selected['stockSources']!.toList()..sort(),
    'interests': _selected['interests']!.toList()..sort(),
    'communicationPreferences': _selected['communicationPreferences']!.toList()
      ..sort(),
    'marketingOptIn': _marketingOptIn,
    'accuracyDeclaration': _registrationDeclaredAccurate,
  };

  Future<void> _submit() async {
    if (_isLoading || !_validateCurrentStep()) return;
    final fullName =
        '${_controller('firstName').text.trim()} ${_controller('surname').text.trim()}';

    setState(() => _isLoading = true);
    try {
      final failedDocuments = await context.read<AuthProvider>().registerShop(
        phone: _controller('phone').text.trim(),
        password: _controller('password').text,
        fullName: fullName,
        shopName: _controller('shopName').text.trim(),
        address: _controller('address').text.trim(),
        city: _controller('city').text.trim(),
        province: _controller('province').text.trim(),
        postalCode: _controller('postalCode').text.trim(),
        latitude: _address?.latitude,
        longitude: _address?.longitude,
        email: _controller('email').text.trim(),
        registrationDetails: _registrationDetails(),
        documents: _pickedDocs,
      );

      if (!mounted) return;
      if (failedDocuments.isNotEmpty) {
        _showMessage(
          '${failedDocuments.length} document(s) could not be uploaded. You can retry from your profile.',
        );
      }
      Navigator.pushNamedAndRemoveUntil(context, '/home', (route) => false);
    } catch (e) {
      if (!mounted) return;
      _showMessage(e.toString());
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final pages = [
      _buildRepresentative(),
      _buildShopDetails(),
      _buildLocation(),
      _buildOperations(),
      _buildReviewAndDocuments(),
    ];

    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [Color(0xFF0A2E0C), Color(0xFF144417), Color(0xFF1B5E20)],
          ),
        ),
        child: SafeArea(
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 20,
                  vertical: 12,
                ),
                child: Row(
                  children: [
                    IconButton(
                      onPressed: _prevStep,
                      icon: const Icon(Icons.arrow_back_rounded),
                      color: Colors.white,
                      tooltip: 'Go back',
                    ),
                    Expanded(
                      child: Text(
                        'Shop owner application',
                        textAlign: TextAlign.center,
                        style: GoogleFonts.nunito(
                          color: Colors.white,
                          fontSize: 18,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                    const SizedBox(width: 48),
                  ],
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(24, 4, 24, 14),
                child: _StepIndicator(
                  currentStep: _currentStep,
                  labels: _stepLabels,
                ),
              ),
              Expanded(
                child: PageView(
                  controller: _pageController,
                  physics: const NeverScrollableScrollPhysics(),
                  children: pages,
                ),
              ),
              Padding(
                padding: const EdgeInsets.fromLTRB(24, 12, 24, 18),
                child: Row(
                  children: [
                    if (_currentStep > 0)
                      OutlinedButton(
                        onPressed: _isLoading ? null : _prevStep,
                        style: OutlinedButton.styleFrom(
                          foregroundColor: Colors.white,
                          side: BorderSide(
                            color: Colors.white.withValues(alpha: 0.65),
                          ),
                          minimumSize: const Size(100, 52),
                        ),
                        child: const Text('Back'),
                      ),
                    if (_currentStep > 0) const SizedBox(width: 12),
                    Expanded(
                      child: SizedBox(
                        height: 52,
                        child: ElevatedButton(
                          onPressed: _isLoading ? null : _nextStep,
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppColors.primary,
                            foregroundColor: Colors.white,
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(14),
                            ),
                          ),
                          child: _isLoading
                              ? const SizedBox(
                                  height: 22,
                                  width: 22,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : Text(
                                  _currentStep == _stepLabels.length - 1
                                      ? 'Submit application'
                                      : 'Continue',
                                  style: GoogleFonts.nunito(
                                    fontSize: 16,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildRepresentative() => _page(
    title: 'Representative details',
    subtitle: 'Who is responsible for this shop application?',
    icon: Icons.person_outline_rounded,
    children: [
      _field('firstName', 'First name *', 'Enter first name'),
      _field('surname', 'Surname *', 'Enter surname'),
      _dropdown(
        label: 'Your role in the business',
        value: _representativeRole,
        options: const [
          'Owner',
          'Co-owner',
          'Manager',
          'Authorised representative',
        ],
        onChanged: (value) => setState(() => _representativeRole = value),
      ),
      _field(
        'phone',
        'Mobile number *',
        'e.g. 082 123 4567',
        keyboardType: TextInputType.phone,
        prefixText: '+27  ',
      ),
      _field(
        'alternatePhone',
        'Alternative contact number',
        'Optional',
        keyboardType: TextInputType.phone,
      ),
      _field(
        'email',
        'Email address',
        'Optional, recommended for account recovery',
        keyboardType: TextInputType.emailAddress,
      ),
      _dropdown(
        label: 'Preferred language',
        value: _preferredLanguage,
        options: _languages,
        onChanged: (value) => setState(() => _preferredLanguage = value),
      ),
      _passwordField(
        keyName: 'password',
        label: 'Create password *',
        obscure: _obscurePassword,
        onToggle: () => setState(() => _obscurePassword = !_obscurePassword),
      ),
      _passwordField(
        keyName: 'confirmPassword',
        label: 'Confirm password *',
        obscure: _obscureConfirmPassword,
        onToggle: () =>
            setState(() => _obscureConfirmPassword = !_obscureConfirmPassword),
      ),
      _hint(
        'Your mobile number and password are used to sign in. We are not sending SMS verification codes during signup.',
        Icons.lock_outline,
      ),
    ],
  );

  String? _representativeRole;
  String? _province;

  Widget _buildShopDetails() => _page(
    title: 'Shop identity',
    subtitle: 'Tell us about the business you want to register.',
    icon: Icons.storefront_outlined,
    children: [
      _field('shopName', 'Shop name *', 'Name customers know'),
      _field('tradingName', 'Trading name', 'If different from shop name'),
      _dropdown(
        label: 'Business type *',
        value: _shopType,
        options: _shopTypes,
        onChanged: (value) => setState(() => _shopType = value),
      ),
      _dropdown(
        label: 'Business registration status *',
        value: _registrationStatus,
        options: const [
          'Registered business',
          'Registration in progress',
          'Not registered',
          'Not sure',
        ],
        onChanged: (value) => setState(() => _registrationStatus = value),
      ),
      if (_registrationStatus == 'Registered business' ||
          _registrationStatus == 'Registration in progress')
        _field(
          'companyRegistrationNumber',
          'Company registration number',
          'Enter if available',
        ),
      _hint(
        'You can submit the application if you are still gathering supporting documents. Missing documents can be added later.',
        Icons.info_outline,
      ),
    ],
  );

  Widget _buildLocation() => _page(
    title: 'Shop location',
    subtitle:
        'Add the physical location so customers and suppliers can find you.',
    icon: Icons.location_on_outlined,
    children: [
      AddressAutocompleteField(
        controller: _controller('address'),
        confirmed: _address != null,
        onSelected: (suggestion) {
          setState(() {
            _address = suggestion;
            if (suggestion.city.isNotEmpty) {
              _controller('city').text = suggestion.city;
              _controller('municipality').text = suggestion.city;
            }
            if (suggestion.province.isNotEmpty) {
              _province = _provinces.cast<String?>().firstWhere(
                (province) =>
                    province?.toLowerCase() ==
                    suggestion.province.toLowerCase(),
                orElse: () => null,
              );
              _controller('province').text = _province ?? '';
            }
            if (suggestion.postalCode.isNotEmpty) {
              _controller('postalCode').text = suggestion.postalCode;
            }
            if (suggestion.suburb.isNotEmpty) {
              _controller('suburbTownship').text = suggestion.suburb;
            }
          });
        },
        onEdited: () {
          if (_address != null) setState(() => _address = null);
        },
      ),
      const SizedBox(height: 14),
      _field('municipality', 'Municipality', 'e.g. eThekwini'),
      _field('city', 'Town or city *', 'Enter town or city'),
      _field('suburbTownship', 'Suburb or township', 'Optional'),
      _field('standNumber', 'Street, stand or unit number', 'Optional'),
      _dropdown(
        label: 'Province *',
        value: _province,
        options: _provinces,
        onChanged: (value) {
          setState(() {
            _province = value;
            _controller('province').text = value ?? '';
          });
        },
      ),
      _field(
        'postalCode',
        'Postal code',
        'Optional',
        keyboardType: TextInputType.number,
      ),
      _hint(
        'You can select an address suggestion or enter the address manually. Precise map coordinates are saved only when a suggestion is selected.',
        Icons.map_outlined,
      ),
    ],
  );

  Widget _buildOperations() => _page(
    title: 'Shop operations',
    subtitle: 'Help us tailor ordering, delivery and support to your business.',
    icon: Icons.inventory_2_outlined,
    children: [
      _multiSelect(
        title: 'Products you sell',
        options: _productCategories,
        selected: _selected['productCategories']!,
      ),
      _dropdown(
        label: 'Approximate monthly stock budget',
        value: _monthlyStockBudget,
        options: const [
          'Under R2,000',
          'R2,000–R5,000',
          'R5,001–R10,000',
          'R10,001–R25,000',
          'Above R25,000',
          'Prefer not to say',
        ],
        onChanged: (value) => setState(() => _monthlyStockBudget = value),
      ),
      _multiSelect(
        title: 'Where do you currently source stock?',
        options: _stockSources,
        selected: _selected['stockSources']!,
      ),
      _field(
        'averageDailyCustomers',
        'Approximate customers per day',
        'Optional',
        keyboardType: TextInputType.number,
      ),
      _multiSelect(
        title: 'Usual operating days',
        options: _days,
        selected: _selected['operatingDays']!,
      ),
      Row(
        children: [
          Expanded(
            child: _field(
              'openingTime',
              'Opening time',
              'e.g. 07:00',
              keyboardType: TextInputType.datetime,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: _field(
              'closingTime',
              'Closing time',
              'e.g. 19:00',
              keyboardType: TextInputType.datetime,
            ),
          ),
        ],
      ),
      SwitchListTile.adaptive(
        contentPadding: EdgeInsets.zero,
        title: const Text('Do you offer local delivery?'),
        value: _deliveryAvailable,
        activeTrackColor: AppColors.primary,
        onChanged: (value) => setState(() => _deliveryAvailable = value),
      ),
      if (_deliveryAvailable)
        _field(
          'deliveryRadiusKm',
          'Delivery area (km)',
          'Approximate radius',
          keyboardType: TextInputType.number,
        ),
      SwitchListTile.adaptive(
        contentPadding: EdgeInsets.zero,
        title: const Text('Do you have refrigeration or cold storage?'),
        value: _refrigerationAvailable,
        activeTrackColor: AppColors.primary,
        onChanged: (value) => setState(() => _refrigerationAvailable = value),
      ),
      _field(
        'storageNotes',
        'Storage or operating notes',
        'Optional details such as freezer or storeroom availability',
        maxLines: 3,
      ),
    ],
  );

  Widget _buildReviewAndDocuments() => _page(
    title: 'Review and submit',
    subtitle:
        'Upload any available documents and choose what updates you want.',
    icon: Icons.fact_check_outlined,
    children: [
      _hint(
        'Applications are submitted as pending review. You can use the dashboard while the application is reviewed and upload missing documents later.',
        Icons.pending_actions_outlined,
      ),
      const SizedBox(height: 8),
      ..._docTypes.entries.map(
        (entry) => _documentTile(entry.key, entry.value),
      ),
      const SizedBox(height: 8),
      _multiSelect(
        title: 'What would you like to use SpazaSure for?',
        options: _interests,
        selected: _selected['interests']!,
      ),
      _multiSelect(
        title: 'Service and notification updates',
        options: _communications,
        selected: _selected['communicationPreferences']!,
      ),
      SwitchListTile.adaptive(
        contentPadding: EdgeInsets.zero,
        title: const Text('Send me optional offers and promotions'),
        subtitle: const Text(
          'Separate from important service and safety updates',
        ),
        value: _marketingOptIn,
        activeTrackColor: AppColors.primary,
        onChanged: (value) => setState(() => _marketingOptIn = value),
      ),
      CheckboxListTile(
        contentPadding: EdgeInsets.zero,
        controlAffinity: ListTileControlAffinity.leading,
        title: const Text('I confirm the information provided is accurate. *'),
        value: _registrationDeclaredAccurate,
        activeColor: AppColors.primary,
        onChanged: (value) =>
            setState(() => _registrationDeclaredAccurate = value ?? false),
      ),
      _hint(
        'Terms and Privacy acknowledgements will be added when the finalized policy documents and links are available.',
        Icons.policy_outlined,
      ),
      const SizedBox(height: 8),
      _reviewSummary(),
    ],
  );

  Widget _page({
    required String title,
    required String subtitle,
    required IconData icon,
    required List<Widget> children,
  }) {
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(20, 8, 20, 20),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(22),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withValues(alpha: 0.14),
              blurRadius: 25,
              offset: const Offset(0, 10),
            ),
          ],
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  backgroundColor: const Color(0xFFE8F5E9),
                  foregroundColor: AppColors.primary,
                  child: Icon(icon),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: GoogleFonts.nunito(
                          fontSize: 19,
                          fontWeight: FontWeight.w800,
                          color: const Color(0xFF1A1A1A),
                        ),
                      ),
                      Text(
                        subtitle,
                        style: GoogleFonts.nunito(
                          fontSize: 12,
                          color: const Color(0xFF6B7280),
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 22),
            ...children.map(
              (child) => Padding(
                padding: const EdgeInsets.only(bottom: 14),
                child: child,
              ),
            ),
          ],
        ),
      ).animate().fadeIn(duration: 250.ms).slideY(begin: 0.03, end: 0),
    );
  }

  Widget _field(
    String keyName,
    String label,
    String hint, {
    TextInputType? keyboardType,
    String? prefixText,
    int maxLines = 1,
  }) {
    return TextField(
      controller: _controller(keyName),
      keyboardType: keyboardType,
      maxLines: maxLines,
      obscureText: keyName == 'password'
          ? _obscurePassword
          : keyName == 'confirmPassword'
          ? _obscureConfirmPassword
          : false,
      decoration: InputDecoration(
        labelText: label,
        hintText: hint,
        prefixText: prefixText,
        filled: true,
        fillColor: const Color(0xFFF7F8F7),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: Color(0xFFE2E6E2)),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
        ),
      ),
    );
  }

  Widget _passwordField({
    required String keyName,
    required String label,
    required bool obscure,
    required VoidCallback onToggle,
  }) => TextField(
    controller: _controller(keyName),
    obscureText: obscure,
    decoration: InputDecoration(
      labelText: label,
      prefixIcon: const Icon(Icons.lock_outline),
      suffixIcon: IconButton(
        onPressed: onToggle,
        icon: Icon(
          obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined,
        ),
      ),
      filled: true,
      fillColor: const Color(0xFFF7F8F7),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
    ),
  );

  Widget _dropdown({
    required String label,
    required String? value,
    required List<String> options,
    required ValueChanged<String?> onChanged,
  }) {
    return DropdownButtonFormField<String>(
      initialValue: value,
      isExpanded: true,
      decoration: InputDecoration(
        labelText: label,
        filled: true,
        fillColor: const Color(0xFFF7F8F7),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(12)),
      ),
      items: options
          .map((option) => DropdownMenuItem(value: option, child: Text(option)))
          .toList(),
      onChanged: onChanged,
    );
  }

  Widget _multiSelect({
    required String title,
    required List<String> options,
    required Set<String> selected,
  }) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
        const SizedBox(height: 6),
        Wrap(
          spacing: 7,
          runSpacing: 2,
          children: options
              .map(
                (option) => FilterChip(
                  label: Text(option),
                  selected: selected.contains(option),
                  selectedColor: const Color(0xFFDFF1E2),
                  checkmarkColor: AppColors.primary,
                  onSelected: (checked) => setState(() {
                    if (checked) {
                      selected.add(option);
                    } else {
                      selected.remove(option);
                    }
                  }),
                ),
              )
              .toList(),
        ),
      ],
    );
  }

  Widget _hint(String text, IconData icon) => Container(
    padding: const EdgeInsets.all(12),
    decoration: BoxDecoration(
      color: const Color(0xFFF0F7F1),
      borderRadius: BorderRadius.circular(12),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, color: AppColors.primary, size: 19),
        const SizedBox(width: 9),
        Expanded(
          child: Text(text, style: const TextStyle(fontSize: 12, height: 1.4)),
        ),
      ],
    ),
  );

  Widget _documentTile(String docType, String title) {
    final file = _pickedDocs[docType];
    return InkWell(
      borderRadius: BorderRadius.circular(12),
      onTap: () => _pickDocument(docType),
      child: Container(
        margin: const EdgeInsets.only(bottom: 8),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          border: Border.all(
            color: file == null
                ? const Color(0xFFE0E5E0)
                : AppColors.primary.withValues(alpha: 0.5),
          ),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Row(
          children: [
            Icon(
              file == null ? Icons.upload_file_outlined : Icons.check_circle,
              color: file == null ? Colors.blueGrey : AppColors.primary,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                  Text(
                    file?.name ?? 'Optional — upload now or later',
                    style: const TextStyle(
                      fontSize: 12,
                      color: Colors.blueGrey,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right),
          ],
        ),
      ),
    );
  }

  Widget _reviewSummary() {
    final shop = _controller('shopName').text.trim();
    final location = [
      _controller('suburbTownship').text.trim(),
      _controller('city').text.trim(),
      _controller('province').text.trim(),
    ].where((part) => part.isNotEmpty).join(', ');
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFFF7F8F7),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Application summary',
            style: TextStyle(fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 8),
          Text('Shop: ${shop.isEmpty ? 'Not entered' : shop}'),
          Text(
            'Representative: ${_controller('firstName').text} ${_controller('surname').text}',
          ),
          Text('Location: ${location.isEmpty ? 'Not entered' : location}'),
          Text('Documents attached: ${_pickedDocs.length}'),
          const SizedBox(height: 6),
          const Text(
            'After submission your application will show as pending review. Your account remains available while review is in progress.',
            style: TextStyle(fontSize: 12, color: Colors.blueGrey),
          ),
        ],
      ),
    );
  }

  Future<void> _pickDocument(String docType) async {
    try {
      final result = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['pdf', 'jpg', 'jpeg', 'png', 'doc', 'docx'],
        withData: true,
      );
      if (result != null && result.files.isNotEmpty) {
        setState(() => _pickedDocs[docType] = result.files.first);
      }
    } catch (error) {
      if (mounted) _showMessage('Could not select the document: $error');
    }
  }
}

class _StepIndicator extends StatelessWidget {
  const _StepIndicator({required this.currentStep, required this.labels});

  final int currentStep;
  final List<String> labels;

  @override
  Widget build(BuildContext context) => Row(
    children: List.generate(labels.length, (index) {
      final active = index <= currentStep;
      return Expanded(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 3),
          child: Column(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(5),
                child: LinearProgressIndicator(
                  value: active ? 1 : 0,
                  minHeight: 4,
                  backgroundColor: Colors.white.withValues(alpha: 0.2),
                  color: const Color(0xFF69C46D),
                ),
              ),
              const SizedBox(height: 5),
              Text(
                labels[index],
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: 9,
                  color: active ? Colors.white : Colors.white60,
                  fontWeight: active ? FontWeight.w700 : FontWeight.w400,
                ),
              ),
            ],
          ),
        ),
      );
    }),
  );
}
