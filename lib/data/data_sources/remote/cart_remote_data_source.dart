import 'package:firebase_database/firebase_database.dart';
import 'package:flutter/cupertino.dart';

import '../../../../core/error/exceptions.dart';
import '../../models/cart/cart_item_model.dart';

abstract class CartRemoteDataSource {
  Future<CartItemModel> addToCart(CartItemModel cartItem, String token);
  Future<List<CartItemModel>> syncCart(List<CartItemModel> cart, String token);
  Future<void> deleteCart(String cartItemId, String userId);
}

class CartRemoteDataSourceSourceImpl implements CartRemoteDataSource {
  final DatabaseReference firebaseDatabase;

  CartRemoteDataSourceSourceImpl({required this.firebaseDatabase});

  @override
  Future<CartItemModel> addToCart(CartItemModel cartItem, String userId) async {
    try {
      // Generate a unique key for the cart item
      final path = 'users/$userId/cart/${cartItem.id}';

      DataSnapshot snapshot = await firebaseDatabase.child(path).get();
      if (snapshot.exists) {
        int quantity = (snapshot.value as Map)['quantity'] ?? 0;
        await firebaseDatabase.child(path).set(cartItem
            .copyWith(quantity: (cartItem.quantity + quantity))
            .toJson());
      } else {
        await firebaseDatabase
            .child('users/$userId/cart/${cartItem.id}')
            .set(cartItem.copyWith(id: cartItem.id).toJson());
      }

      /*final cartItemId =
          firebaseDatabase.child('users/$userId/cart').push().key;

      if (cartItemId == null) {
        throw ServerException();
      }

      // Add the cart item to Firebase under the user's cart node
      await firebaseDatabase
          .child('users/$userId/cart/$cartItemId')
          .set(cartItem.copyWith(id: cartItemId).toJson());*/

      // Return the added cart item with the generated ID
      return cartItem.copyWith(id: cartItem.id);
    } catch (e, s) {
      print('error $e $s');
      throw ServerException();
    }
  }

  @override
  Future<List<CartItemModel>> syncCart(
      List<CartItemModel> cart, String userId) async {
    try {
      // Replace the entire cart with the provided list
      final cartRef = firebaseDatabase.child('users/$userId/cart');
      await cartRef.remove(); // Clear the existing cart

      // Add the new cart items to Firebase
      for (var cartItem in cart) {
        final cartItemId = cartRef.push().key;
        if (cartItemId == null) {
          throw ServerException();
        }

        await cartRef.child(cartItemId).set(cartItem.toBodyJson());
      }

      // Retrieve the updated cart from Firebase
      final snapshot = await cartRef.get();

      if (snapshot.exists) {
        final cartItems = (snapshot.value as Map).entries.map((entry) {
          return CartItemModel.fromJson({
            'id': entry.key,
            ...Map<String, dynamic>.from(entry.value as Map)
          });
        }).toList();

        return cartItems;
      } else {
        return [];
      }
    } catch (e) {
      throw ServerException();
    }
  }

  @override
  Future<void> deleteCart(String cartItemId, String userId) async {
    try {
      // Add the cart item to Firebase under the user's cart node
      await firebaseDatabase.child('users/$userId/cart/$cartItemId').remove();

      // Return the added cart item with the generated ID
    } catch (e) {
      debugPrint('deleteCart $e');
      throw ServerException();
    }
  }
}
