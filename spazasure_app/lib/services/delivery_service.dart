import 'api_service.dart';

/// Where an order is: the branch it ships from, the shop, and (while it's on the road) the
/// driver's latest position, as shared from the driver's phone.
class DeliveryTracking {
  final String status;
  final TrackingPlace? pickup;
  final TrackingPlace? destination;
  final TrackingDriver? driver;
  final double? driverLatitude;
  final double? driverLongitude;
  final DateTime? driverUpdatedAt;

  const DeliveryTracking({
    required this.status,
    this.pickup,
    this.destination,
    this.driver,
    this.driverLatitude,
    this.driverLongitude,
    this.driverUpdatedAt,
  });

  bool get hasLiveLocation => driverLatitude != null && driverLongitude != null;

  factory DeliveryTracking.fromJson(Map<String, dynamic> json) {
    final live = json['driverLocation'] as Map<String, dynamic>?;
    final driver = json['driver'] as Map<String, dynamic>?;
    return DeliveryTracking(
      status: json['status']?.toString() ?? '',
      pickup: TrackingPlace.fromJson(json['pickup'] as Map<String, dynamic>?, nameKey: 'name', addressKey: 'address'),
      destination: TrackingPlace.fromJson(json['destination'] as Map<String, dynamic>?, nameKey: null, addressKey: 'deliveryAddress'),
      driver: driver == null
          ? null
          : TrackingDriver(
              fullName: driver['fullName']?.toString() ?? '',
              phone: driver['phone']?.toString() ?? '',
              vehicleType: driver['vehicleType']?.toString() ?? '',
              vehicleRegistration: driver['vehicleRegistration']?.toString() ?? '',
              badgeCode: driver['badgeCode']?.toString() ?? '',
            ),
      driverLatitude: (live?['latitude'] as num?)?.toDouble(),
      driverLongitude: (live?['longitude'] as num?)?.toDouble(),
      driverUpdatedAt: DateTime.tryParse(live?['updatedAt']?.toString() ?? '')?.toLocal(),
    );
  }
}

class TrackingPlace {
  final String name;
  final String address;
  final double? latitude;
  final double? longitude;

  const TrackingPlace({required this.name, required this.address, this.latitude, this.longitude});

  static TrackingPlace? fromJson(Map<String, dynamic>? json, {required String? nameKey, required String addressKey}) {
    if (json == null) return null;
    return TrackingPlace(
      name: nameKey == null ? '' : json[nameKey]?.toString() ?? '',
      address: json[addressKey]?.toString() ?? '',
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
    );
  }
}

class TrackingDriver {
  final String fullName;
  final String phone;
  final String vehicleType;
  final String vehicleRegistration;

  /// Shops can check this with SpazaSure's verification page before handing over cash.
  final String badgeCode;

  const TrackingDriver({
    required this.fullName,
    required this.phone,
    required this.vehicleType,
    required this.vehicleRegistration,
    required this.badgeCode,
  });
}

class DeliveryService {
  static Future<DeliveryTracking> tracking(String orderId) async {
    final res = await ApiService.get('/delivery/orders/$orderId/tracking');
    return DeliveryTracking.fromJson(res['data'] as Map<String, dynamic>);
  }
}
