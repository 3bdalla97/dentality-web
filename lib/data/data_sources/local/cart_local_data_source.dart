import 'package:dental/core/error/failures.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../../models/cart/cart_item_model.dart';

abstract class CartLocalDataSource {
  Future<List<CartItemModel>> getCart();
  Future<void> saveCart(List<CartItemModel> cart);
  Future<void> saveCartItem(CartItemModel cartItem);
  Future<void> deleteCartItem(String cartItemId);
  Future<bool> clearCart();
}

const cachedCart = 'CACHED_CART';

class CartLocalDataSourceImpl implements CartLocalDataSource {
  final SharedPreferences sharedPreferences;
  CartLocalDataSourceImpl({required this.sharedPreferences});

  @override
  Future<void> saveCart(List<CartItemModel> cart) {
    return sharedPreferences.setString(
      cachedCart,
      cartItemModelToJson(cart),
    );
  }

  @override
  Future<void> saveCartItem(CartItemModel cartItem) {
    final jsonString = sharedPreferences.getString(cachedCart);
    final List<CartItemModel> cart = [];
    if (jsonString != null) {
      cart.addAll(cartItemModelListFromLocalJson(jsonString));
    }
    int index = cart.indexWhere((element) =>
        element.product.id == cartItem.product.id &&
        element.priceTag.id == cartItem.priceTag.id);
    if (index != -1) {
      CartItemModel item = cart[index];
      int currentQuantity = item.quantity;
      cart[index] =
          item.copyWith(quantity: currentQuantity + cartItem.quantity);
    } else {
      cart.add(cartItem);
    }
    /*if (!cart.any((element) =>
        element.product.id == cartItem.product.id &&
        element.priceTag.id == cartItem.priceTag.id)) {
      cart.add(cartItem);
    }*/
    return sharedPreferences.setString(
      cachedCart,
      cartItemModelToJson(cart),
    );
  }

  @override
  Future<List<CartItemModel>> getCart() {
    final jsonString = sharedPreferences.getString(cachedCart);
    if (jsonString != null) {
      return Future.value(cartItemModelListFromLocalJson(jsonString));
    } else {
      throw CacheFailure();
    }
  }

  @override
  Future<bool> clearCart() async {
    return sharedPreferences.remove(cachedCart);
  }

  @override
  Future<void> deleteCartItem(String cartItemId) {
    final jsonString = sharedPreferences.getString(cachedCart);
    if (jsonString != null) {
      List<CartItemModel> cart = cartItemModelListFromLocalJson(jsonString);
      cart.removeWhere((item) => item.id == cartItemId);
      return sharedPreferences.setString(
        cachedCart,
        cartItemModelToJson(cart),
      );
    } else {
      throw CacheFailure();
    }
  }
}
