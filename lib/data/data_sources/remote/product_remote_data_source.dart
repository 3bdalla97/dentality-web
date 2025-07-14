import 'dart:convert';

import 'package:dental/core/error/failures.dart';
import 'package:dental/data/models/category/category_model.dart';
import 'package:dental/data/models/product/price_tag_model.dart';
import 'package:dental/data/models/product/product_model.dart';
import 'package:dental/domain/entities/category/category.dart';
import 'package:dental/domain/entities/product/product.dart';
import 'package:firebase_database/firebase_database.dart';
import 'package:http/http.dart' as http;

import '../../../../core/error/exceptions.dart';
import '../../../core/constant/strings.dart';
import '../../../domain/usecases/product/get_product_usecase.dart';
import '../../models/product/product_response_model.dart';

abstract class ProductRemoteDataSource {
  Future<ProductResponseModel> getProducts(FilterProductParams params);
}

class ProductRemoteDataSourceImpl implements ProductRemoteDataSource {
  final DatabaseReference databaseReference;

  ProductRemoteDataSourceImpl({required this.databaseReference});

  @override
  Future<ProductResponseModel> getProducts(params) async {
    return await _getProductsFromFirebase(
      keyword: params.keyword,
      categories: params.categories,
      subcategories: params.subCategories,
    );
  }

  Future<ProductResponseModel> _getProductsFromFirebase({
    required String? keyword,
    required List<Category> categories,
    required List<Category> subcategories,
  }) async {
    try {
      final snapshot = await databaseReference.child('products').get();

      if (snapshot.exists) {
        final products = <ProductModel>[];

        for (final child in snapshot.children) {
          try {
            // Get the key and value of the current child
            final productData = Map<String, dynamic>.from(child.value as Map);

            // Parse categories list to List<CategoryModel>
            final categoriesList = (productData['categories'] as List)
                .map((category) => CategoryModel.fromJson(
                    Map<String, dynamic>.from(category as Map)))
                .toList();
            // Parse categories list to List<CategoryModel>
            final subCategoriesList = (productData['subcategories'] as List)
                .map((subcategory) => CategoryModel.fromJson(
                    Map<String, dynamic>.from(subcategory as Map)))
                .toList();

            // Parse price tags list to List<PriceTagModel>
            final priceTagsList = (productData['priceTags'] as List)
                .map((priceTag) => PriceTagModel.fromJson(
                    Map<String, dynamic>.from(priceTag as Map)))
                .toList();

            // Parse the product data
            final product = ProductModel(
              id: productData['_id'] as String,
              name: productData['name'] as String,
              description: productData['description'] as String,
              priceTags: priceTagsList,
              categories: categoriesList,
              subcategories: subCategoriesList,
              images: List<String>.from(productData['images'] as List),
              createdAt: DateTime.parse(productData['createdAt'] as String),
              updatedAt: DateTime.parse(productData['updatedAt'] as String),
            );

            // Filter by keyword or categories
            final matchesKeyword = keyword == null ||
                product.name.toLowerCase().contains(keyword.toLowerCase()) ||
                product.description
                    .toLowerCase()
                    .contains(keyword.toLowerCase());

            final matchesCategory = categories.isEmpty ||
                categories.any((cat) =>
                    product.categories.any((prodCat) => prodCat.id == cat.id));

            final matchesSubCategory = categories.isEmpty ||
                categories.any((cat) => product.subcategories
                    .any((prodCat) => prodCat.id == cat.id));

            if (matchesKeyword && matchesCategory ||
                matchesKeyword && matchesSubCategory) {
              products.add(product);
            }
          } catch (e) {
            // Log the error and skip the invalid product
            print('Error processing product with key: ${child.key}');
            print('Exception: $e');
          }
        }

        return ProductResponseModel(data: products);
      } else {
        return ProductResponseModel(
            data: []); // Return empty list if no products exist
      }
    } catch (e) {
      print('Error fetching products: $e');
      throw ServerException(); // Handle Firebase-specific errors
    }
  }
}
