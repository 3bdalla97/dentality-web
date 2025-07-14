import 'package:equatable/equatable.dart';

class Category extends Equatable {
  final String id;
  final String name;
  final String image;
  final Map<String, Category>? subcategories;

  const Category({
    required this.id,
    required this.name,
    required this.image,
    this.subcategories,
  });

  @override
  List<Object?> get props => [id];
}
