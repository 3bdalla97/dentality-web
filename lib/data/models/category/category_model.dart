import 'dart:convert';

import '../../../domain/entities/category/category.dart';

List<CategoryModel> categoryModelListFromRemoteJson(String str) =>
    List<CategoryModel>.from(
        json.decode(str)['data'].map((x) => CategoryModel.fromJson(x)));

List<CategoryModel> categoryModelListFromLocalJson(String str) =>
    List<CategoryModel>.from(
        json.decode(str).map((x) => CategoryModel.fromJson(x)));

String categoryModelListToJson(List<CategoryModel> data) =>
    json.encode(List<dynamic>.from(data.map((x) => x.toJson())));

class CategoryModel extends Category {
  const CategoryModel({
    required String id,
    required String name,
    required String image,
    Map<String, CategoryModel>? subcategories,
  }) : super(
          id: id,
          name: name,
          image: image,
          subcategories: subcategories,
        );

  factory CategoryModel.fromJson(Map<String, dynamic> json) {
    Map<String, CategoryModel>? subcategories;
    if (json['subcategories'] != null) {
      subcategories = (json['subcategories'] as Map).map(
        (key, value) => MapEntry(
          key as String,
          CategoryModel.fromJson(Map<String, dynamic>.from(value as Map)),
        ),
      );
    }
    return CategoryModel(
      id: json["id"],
      name: json["name"],
      image: json["image"],
      subcategories: subcategories,
    );
  }

  Map<String, dynamic> toJson() => {
        "id": id,
        "name": name,
        "image": image,
        "subcategories":
            subcategories?.map((key, value) => MapEntry(key, value)),
      };

  factory CategoryModel.fromEntity(Category entity) => CategoryModel(
        id: entity.id,
        name: entity.name,
        image: entity.image,
        subcategories: entity.subcategories?.map(
            (key, value) => MapEntry(key, CategoryModel.fromEntity(value))),
      );
}
