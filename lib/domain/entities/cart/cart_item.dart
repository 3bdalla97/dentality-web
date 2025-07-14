import 'package:equatable/equatable.dart';

import '../product/price_tag.dart';
import '../product/product.dart';

class CartItem extends Equatable {
  final String? id;
  final Product product;
  final PriceTag priceTag;
  final int quantity;

  const CartItem({
    this.id,
    required this.product,
    required this.priceTag,
    required this.quantity,
  });

  @override
  List<Object?> get props => [id];
}
