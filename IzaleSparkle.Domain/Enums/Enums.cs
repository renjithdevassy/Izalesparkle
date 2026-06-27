namespace IzaleSparkle.Domain.Enums;

public enum ProductCategory { Rings, Necklaces, Earrings, Bracelets }
public enum MetalType { WhiteGold18K, YellowGold18K, RoseGold18K, Platinum }
public enum BadgeType { New, Sale, Bestseller }
public enum OrderStatus { Pending, PaymentReceived, Processing, CollectionReady, Shipped, Delivered, Cancelled, Refunded }
public enum PaymentMethod { Card, PayPal, ApplePay }
public enum ShippingTier { Collection, Standard, Express }

public enum UserRole { Customer, Admin }
