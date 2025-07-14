import 'package:dental/data/models/category/category_model.dart';
import 'package:dental/data/models/order/order_item_model.dart';
import 'package:dental/data/models/product/price_tag_model.dart';
import 'package:dental/data/models/product/product_model.dart';
import 'package:dental/data/models/user/delivery_info_model.dart';
import 'package:firebase_database/firebase_database.dart';

import '../../../core/error/exceptions.dart';
import '../../models/order/order_details_model.dart';

abstract class OrderRemoteDataSource {
  Future<OrderDetailsModel> addOrder(OrderDetailsModel params, String token);
  Future<List<OrderDetailsModel>> getOrders(String token);
}

class OrderRemoteDataSourceSourceImpl implements OrderRemoteDataSource {
  final DatabaseReference firebaseDatabase;

  OrderRemoteDataSourceSourceImpl({required this.firebaseDatabase});

  @override
  Future<OrderDetailsModel> addOrder(
      OrderDetailsModel params, String userId) async {
    try {
      // Create a unique ID for the order
      final orderId = firebaseDatabase.child('users/$userId/orders').push().key;

      if (orderId == null) {
        throw ServerException();
      }

      // Convert the order model to JSON and add it to Firebase
      await firebaseDatabase
          .child('users/$userId/orders/$orderId')
          .set(params.copyWith(id: orderId).toJson());

      // Return the added order with the generated ID
      return params.copyWith(id: orderId);
    } catch (e) {
      throw ServerException();
    }
  }

  @override
  Future<List<OrderDetailsModel>> getOrders(String userId) async {
    try {
      final snapshot =
          await firebaseDatabase.child('users/$userId/orders').get();

      if (snapshot.exists) {
        final orders = <OrderDetailsModel>[];

        for (final child in snapshot.children) {
          try {
            // Get the key and value of the current child
            final orderData = Map<String, dynamic>.from(child.value as Map);

            // Parse order items
            final orderItems = (orderData['orderItems'] as List).map((item) {
              final itemData = Map<String, dynamic>.from(item as Map);

              // Parse product
              final productData =
                  Map<String, dynamic>.from(itemData['product'] as Map);
              final product = ProductModel(
                id: productData['_id'] as String,
                name: productData['name'] as String,
                description: productData['description'] as String,
                priceTags: (productData['priceTags'] as List).map((tag) {
                  final tagData = Map<String, dynamic>.from(tag as Map);
                  return PriceTagModel(
                    id: tagData['_id'] as String,
                    name: tagData['name'] as String,
                    price: tagData['price'] as num,
                  );
                }).toList(),
                categories: (productData['categories'] as List).map((cat) {
                  final catData = Map<String, dynamic>.from(cat as Map);
                  return CategoryModel(
                    id: catData['id'] as String,
                    name: catData['name'] as String,
                    image: catData['image'] as String,
                  );
                }).toList(),
                subcategories:
                    (productData['subcategories'] as List).map((cat) {
                  final catData = Map<String, dynamic>.from(cat as Map);
                  return CategoryModel(
                    id: catData['id'] as String,
                    name: catData['name'] as String,
                    image: catData['image'] as String,
                  );
                }).toList(),
                images: List<String>.from(productData['images'] as List),
                createdAt: DateTime.parse(productData['createdAt'] as String),
                updatedAt: DateTime.parse(productData['updatedAt'] as String),
              );

              // Parse price tag
              final priceTagData =
                  Map<String, dynamic>.from(itemData['priceTag'] as Map);
              final priceTag = PriceTagModel(
                id: priceTagData['_id'] as String,
                name: priceTagData['name'] as String,
                price: priceTagData['price'] as num,
              );

              return OrderItemModel(
                id: itemData['_id'] as String,
                product: product,
                priceTag: priceTag,
                price: itemData['price'] as num,
                quantity: itemData['quantity'] as num,
                date: itemData['date'] as String,
              );
            }).toList();

            // Parse delivery info
            final deliveryInfoData =
                Map<String, dynamic>.from(orderData['deliveryInfo'] as Map);
            final deliveryInfo = DeliveryInfoModel(
              id: deliveryInfoData['_id'] as String,
              firstName: deliveryInfoData['firstName'] as String,
              lastName: deliveryInfoData['lastName'] as String,
              addressLineOne: deliveryInfoData['addressLineOne'] as String,
              addressLineTwo: deliveryInfoData['addressLineTwo'] as String,
              city: deliveryInfoData['city'] as String,
              zipCode: deliveryInfoData['zipCode'] as String,
              contactNumber: deliveryInfoData['contactNumber'] as String,
            );

            // Parse order
            final order = OrderDetailsModel(
              id: child.key ?? '', // Use the Firebase key as the ID
              orderItems: orderItems,
              deliveryInfo: deliveryInfo,
              discount: orderData['discount'] as num,
              date: orderData['date'] as String,
            );

            orders.add(order);
          } catch (e) {
            // Log the error and skip invalid orders
            print('Error processing order with key: ${child.key}');
            print('Exception: $e');
          }
        }

        return orders;
      } else {
        return []; // Return empty list if no orders exist
      }
    } catch (e) {
      print('Error fetching orders: $e');
      throw ServerException(); // Handle Firebase-specific errors
    }
  }
}
