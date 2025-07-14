import '../../../domain/entities/product/pagination_meta_data.dart';
import '../../../domain/entities/product/product.dart';
import '../../../domain/entities/product/product_response.dart';
import 'pagination_data_model.dart';
import 'product_model.dart';
import 'dart:convert';

ProductResponseModel productResponseModelFromJson(String str) =>
    ProductResponseModel.fromJson(json.decode(str));

String productResponseModelToJson(ProductResponseModel data) =>
    json.encode(data.toJson());

class ProductResponseModel extends ProductResponse {
  ProductResponseModel({
    required List<Product> data,
  }) : super(products: data);

  factory ProductResponseModel.fromJson(Map<String, dynamic> json) =>
      ProductResponseModel(
        data: List<ProductModel>.from(
            json["products"].map((x) => ProductModel.fromJson(x))),
      );

  Map<String, dynamic> toJson() => {
        "products": List<dynamic>.from(
            (products as List<ProductModel>).map((x) => x.toJson())),
      };
}
