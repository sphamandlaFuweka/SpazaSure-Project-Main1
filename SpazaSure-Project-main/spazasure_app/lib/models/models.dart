// User / Shop Owner
class ShopUser {
  final String id;
  final String fullName;
  final String phone;
  final String email;
  final String shopName;
  final String address;
  final double latitude;
  final double longitude;
  final String
  permitStatus; // registered, docs_under_review, verified, rejected, suspended
  final bool isVerified;
  final String? profileImageUrl;

  const ShopUser({
    required this.id,
    required this.fullName,
    required this.phone,
    this.email = '',
    required this.shopName,
    required this.address,
    this.latitude = 0,
    this.longitude = 0,
    this.permitStatus = 'registered',
    this.isVerified = false,
    this.profileImageUrl,
  });
}

// Product
class Product {
  final String id;
  final String name;
  final String description;
  final String sku;
  final double price;
  final double? discountPrice;
  final String imageUrl;
  final List<String> images;
  final String categoryId;
  final String categoryName;
  final String supplierId;
  final String supplierName;
  final int stockQuantity;
  final int minOrderQty;
  final double rating;
  final int reviewCount;
  final String qrCode;
  final bool isAvailable;
  final bool isNearby;
  final String? supplierCity;

  const Product({
    required this.id,
    required this.name,
    required this.description,
    required this.sku,
    required this.price,
    this.discountPrice,
    required this.imageUrl,
    this.images = const [],
    required this.categoryId,
    required this.categoryName,
    required this.supplierId,
    required this.supplierName,
    this.stockQuantity = 0,
    this.minOrderQty = 1,
    this.rating = 0,
    this.reviewCount = 0,
    this.qrCode = '',
    this.isAvailable = true,
    this.isNearby = false,
    this.supplierCity,
  });
}

// Category
class Category {
  final String id;
  final String name;
  final String iconName;
  final String? parentId;
  final int productCount;

  const Category({
    required this.id,
    required this.name,
    required this.iconName,
    this.parentId,
    this.productCount = 0,
  });
}

// Cart Item
class CartItem {
  final Product product;
  int quantity;

  CartItem({required this.product, this.quantity = 1});

  double get total => product.price * quantity;
}

// Order
class Order {
  final String id;
  final String orderNumber;
  final String shopId;
  final String supplierId;
  final String supplierName;
  final List<OrderItem> items;
  final double subtotal;
  final double deliveryFee;
  final double platformFee;
  final double total;
  final String
  status; // draft, pending, confirmed, processing, dispatched, delivered, cancelled, disputed
  final String deliveryOption; // standard, express, pickup
  final String paymentMethod; // eft, wallet
  final String
  paymentStatus; // initiated, pending, held, released, failed, refunded
  final String? groupBuyId;
  final bool isGroupBuy;
  final String deliveryAddress;
  final double? deliveryDistanceKm;
  final DateTime createdAt;
  final DateTime? estimatedDelivery;

  const Order({
    required this.id,
    required this.orderNumber,
    required this.shopId,
    required this.supplierId,
    required this.supplierName,
    required this.items,
    required this.subtotal,
    required this.deliveryFee,
    required this.platformFee,
    required this.total,
    required this.status,
    required this.deliveryOption,
    required this.paymentMethod,
    required this.paymentStatus,
    this.groupBuyId,
    this.isGroupBuy = false,
    this.deliveryAddress = '',
    this.deliveryDistanceKm,
    required this.createdAt,
    this.estimatedDelivery,
  });
}

// Order Item
class OrderItem {
  final String productId;
  final String productName;
  final String productImage;
  final int quantity;
  final double price;

  const OrderItem({
    required this.productId,
    required this.productName,
    required this.productImage,
    required this.quantity,
    required this.price,
  });

  double get total => price * quantity;
}

// Delivery
class Delivery {
  final String id;
  final String orderId;
  final String driverName;
  final String driverPhone;
  final double driverLat;
  final double driverLng;
  final String status; // assigned, picked_up, in_transit, arriving, delivered
  final DateTime? eta;

  const Delivery({
    required this.id,
    required this.orderId,
    required this.driverName,
    required this.driverPhone,
    this.driverLat = 0,
    this.driverLng = 0,
    required this.status,
    this.eta,
  });
}

// Compliance Document
class ComplianceDoc {
  final String id;
  final String shopId;
  final String docType; // business_permit, health_cert, lease, id_document
  final String docUrl;
  final String status; // pending, approved, rejected
  final DateTime? expiryDate;
  final String? rejectionReason;

  const ComplianceDoc({
    required this.id,
    required this.shopId,
    required this.docType,
    required this.docUrl,
    required this.status,
    this.expiryDate,
    this.rejectionReason,
  });
}

// Supplier
class Supplier {
  final String id;
  final String companyName;
  final String logoUrl;
  final double rating;
  final int reviewCount;
  final int productCount;
  final String tier; // basic, bronze, silver, gold
  final bool isVerified;

  const Supplier({
    required this.id,
    required this.companyName,
    required this.logoUrl,
    this.rating = 0,
    this.reviewCount = 0,
    this.productCount = 0,
    this.tier = 'basic',
    this.isVerified = false,
  });
}

// Notification
class AppNotification {
  final String id;
  final String title;
  final String body;
  final String type; // order, delivery, compliance, system
  final bool isRead;
  final DateTime createdAt;
  final String? actionRoute;

  const AppNotification({
    required this.id,
    required this.title,
    required this.body,
    required this.type,
    this.isRead = false,
    required this.createdAt,
    this.actionRoute,
  });
}
