import 'package:dental/core/error/failures.dart';
import 'package:firebase_database/firebase_database.dart';

import '../../models/category/category_model.dart';

abstract class CategoryRemoteDataSource {
  Future<List<CategoryModel>> getCategories();
}

class CategoryRemoteDataSourceImpl implements CategoryRemoteDataSource {
  final DatabaseReference databaseReference;

  CategoryRemoteDataSourceImpl({required this.databaseReference});

  @override
  Future<List<CategoryModel>> getCategories() => _getCategoriesFromFirebase();

  Future<List<CategoryModel>> _getCategoriesFromFirebase() async {
    try {
      final snapshot = await databaseReference.child('categories').get();

      if (snapshot.exists) {
        final Map<String, dynamic> data =
            Map<String, dynamic>.from(snapshot.value as Map);
        return data.values
            .map((category) =>
                CategoryModel.fromJson(Map<String, dynamic>.from(category)))
            .toList();
      } else {
        return [];
      }
    } catch (e) {
      throw ServerFailure();
    }
  }
}
